package com.likelion.queuemanager.repository;

import com.likelion.queue.common.QueueRedisKeys;
import com.likelion.queuemanager.model.ServerStatus;
import org.springframework.data.redis.core.HashOperations;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.ZSetOperations;
import org.springframework.data.redis.core.script.DefaultRedisScript;
import org.springframework.stereotype.Repository;

import java.time.Duration;
import java.time.Instant;
import java.util.*;

@Repository
public class QueueManagerRepository {

    private static final String WAITING_QUEUE_KEY = QueueRedisKeys.WAITING_QUEUE;
    private static final String WAITING_META_PREFIX = QueueRedisKeys.WAITING_META_PREFIX;
    private static final String JOINING_TICKETS_KEY = QueueRedisKeys.JOINING_TICKETS;
    private static final String JOINING_TICKET_PREFIX = QueueRedisKeys.JOINING_TICKET_PREFIX;
    private static final String SERVER_STATUS_KEY = QueueRedisKeys.SERVER_STATUS;

    private static final DefaultRedisScript<Long> PROMOTE_SCRIPT;

    static {
        PROMOTE_SCRIPT = new DefaultRedisScript<>();
        PROMOTE_SCRIPT.setResultType(Long.class);
        PROMOTE_SCRIPT.setScriptText(
            """
                local waitingKey = KEYS[1]
                local metaKey = KEYS[2]
                local joiningKey = KEYS[3]
                local ticketKey = KEYS[4]
                local userId = ARGV[1]
                local ticketId = ARGV[2]
                local expireAt = tonumber(ARGV[3])
                local ttlSeconds = tonumber(ARGV[4])
                if redis.call('ZSCORE', waitingKey, userId) == false then
                  return 0
                end
                if redis.call('EXISTS', metaKey) == 0 then
                  redis.call('ZREM', waitingKey, userId)
                  return 0
                end
                local nickname = redis.call('HGET', metaKey, 'nickname')
                if not nickname or nickname == '' then
                  redis.call('DEL', metaKey)
                  redis.call('ZREM', waitingKey, userId)
                  return 0
                end
                local storedUserId = redis.call('HGET', metaKey, 'userId')
                if not storedUserId or storedUserId == '' then
                  storedUserId = userId
                end
                redis.call('HSET', ticketKey,
                  'ticketId', ticketId,
                  'userId', storedUserId,
                  'nickname', nickname)
                redis.call('EXPIRE', ticketKey, ttlSeconds)
                redis.call('ZREM', waitingKey, userId)
                redis.call('HSET', metaKey, 'ticketId', ticketId)
                redis.call('ZADD', joiningKey, expireAt, ticketId)
                return 1"""
        );
    }

    private final StringRedisTemplate stringRedisTemplate;
    private final ZSetOperations<String, String> zSetOperations;
    private final HashOperations<String, String, String> hashOperations;

    public QueueManagerRepository(StringRedisTemplate stringRedisTemplate) {
        this.stringRedisTemplate = stringRedisTemplate;
        this.zSetOperations = stringRedisTemplate.opsForZSet();
        this.hashOperations = stringRedisTemplate.opsForHash();
    }

    public List<String> fetchNextBatch(int batchSize) {
        if (batchSize <= 0) {
            return Collections.emptyList();
        }
        Set<String> range = zSetOperations.range(WAITING_QUEUE_KEY, 0, batchSize - 1);
        if (range == null || range.isEmpty()) {
            return Collections.emptyList();
        }
        return new ArrayList<>(range);
    }

    public Long waitingSize() {
        return zSetOperations.zCard(WAITING_QUEUE_KEY);
    }

    public void removeFromWaiting(String userId) {
        zSetOperations.remove(WAITING_QUEUE_KEY, userId);
    }

    public Map<String, String> fetchUserMeta(String userId) {
        return hashOperations.entries(WAITING_META_PREFIX + userId);
    }

    public void deleteWaitingMeta(String userId) {
        stringRedisTemplate.delete(WAITING_META_PREFIX + userId);
    }

    public ServerStatus fetchServerStatus() {
        Map<String, String> fields = hashOperations.entries(SERVER_STATUS_KEY);
        if (fields == null || fields.isEmpty()) {
            return new ServerStatus(0, null, null);
        }
        long currentUsers = parseLong(fields.get("current_users"), 0);
        Long softCap = parseNullableLong(fields.get("soft_cap"));
        Long maxCap = parseNullableLong(fields.get("max_cap"));
        return new ServerStatus(currentUsers, softCap, maxCap);
    }

    public long countJoiningTickets(long fromEpochMillis) {
        Long count = zSetOperations.count(JOINING_TICKETS_KEY, fromEpochMillis, Double.POSITIVE_INFINITY);
        return count != null ? count : 0;
    }

    public Set<String> removeExpiredTickets(long nowEpochMillis) {
        Set<String> expired = zSetOperations.rangeByScore(JOINING_TICKETS_KEY, 0, nowEpochMillis);
        if (expired == null || expired.isEmpty()) {
            return Collections.emptySet();
        }
        zSetOperations.remove(JOINING_TICKETS_KEY, expired.toArray());
        return expired;
    }

    public void deleteTicketHashes(Collection<String> ticketIds) {
        if (ticketIds == null || ticketIds.isEmpty()) {
            return;
        }
        List<String> keys = ticketIds.stream()
            .filter(Objects::nonNull)
            .map(id -> JOINING_TICKET_PREFIX + id)
            .toList();
        if (!keys.isEmpty()) {
            stringRedisTemplate.delete(keys);
        }
    }

    public boolean promoteToJoining(String userId, String ticketId, Instant expireAt, Duration ttl) {
        if (ttl == null || ttl.isZero() || ttl.isNegative()) {
            throw new IllegalArgumentException("Ticket TTL must be positive");
        }
        if (expireAt == null) {
            throw new IllegalArgumentException("Expire timestamp is required");
        }
        long ttlSeconds = Math.max(1, ttl.getSeconds());
        List<String> keys = Arrays.asList(
            WAITING_QUEUE_KEY,
            WAITING_META_PREFIX + userId,
            JOINING_TICKETS_KEY,
            JOINING_TICKET_PREFIX + ticketId
        );
        Long updated = stringRedisTemplate.execute(
            PROMOTE_SCRIPT,
            keys,
            userId,
            ticketId,
            String.valueOf(expireAt.toEpochMilli()),
            String.valueOf(ttlSeconds)
        );
        return updated != null && updated > 0;
    }

    private long parseLong(Object value, long defaultValue) {
        if (value == null) {
            return defaultValue;
        }
        try {
            if (value instanceof Number number) {
                return number.longValue();
            }
            return Long.parseLong(value.toString());
        } catch (NumberFormatException e) {
            return defaultValue;
        }
    }

    private Long parseNullableLong(Object value) {
        if (value == null) {
            return null;
        }
        try {
            if (value instanceof Number number) {
                return number.longValue();
            }
            return Long.parseLong(value.toString());
        } catch (NumberFormatException e) {
            return null;
        }
    }
}
