package com.likelion.queueapi.repository;

import com.likelion.queue.common.QueueRedisKeys;
import org.springframework.data.redis.core.HashOperations;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.ZSetOperations;
import org.springframework.stereotype.Repository;

import java.time.Duration;
import java.time.Instant;
import java.util.HashMap;
import java.util.Map;
import java.util.Objects;
import java.util.Optional;

@Repository
public class QueueRepository {

    private static final String WAITING_QUEUE_KEY = QueueRedisKeys.WAITING_QUEUE;
    private static final String WAITING_META_PREFIX = QueueRedisKeys.WAITING_META_PREFIX;

    private final StringRedisTemplate stringRedisTemplate;
    private final ZSetOperations<String, String> zSetOperations;
    private final HashOperations<String, String, String> hashOperations;

    public QueueRepository(StringRedisTemplate stringRedisTemplate) {
        this.stringRedisTemplate = stringRedisTemplate;
        this.zSetOperations = stringRedisTemplate.opsForZSet();
        this.hashOperations = stringRedisTemplate.opsForHash();
    }

    public boolean addToWaitingQueue(String userId, double score) {
        Boolean result = zSetOperations.add(WAITING_QUEUE_KEY,
                (String) Objects.requireNonNull(userId, "userId must not be null"),
                score);
        return Boolean.TRUE.equals(result);
    }

    public void upsertWaitingMeta(String userId, String nickname, Duration ttl) {
        String metaKey = WAITING_META_PREFIX + userId;
        Map<String, String> payload = new HashMap<>();
        payload.put("userId", userId);
        payload.put("nickname", nickname);
        payload.put("ticketId", "");
        payload.put("lastSeenAt", Instant.now().toString());
        hashOperations.putAll(metaKey, payload);
        if (ttl != null && !ttl.isZero() && !ttl.isNegative()) {
            stringRedisTemplate.expire(metaKey, ttl);
        }
    }

    public Optional<Map<String, String>> findWaitingMeta(String userId) {
        Map<String, String> entries = hashOperations.entries(WAITING_META_PREFIX + userId);
        if (entries == null || entries.isEmpty()) {
            return Optional.empty();
        }
        return Optional.of(entries);
    }

    public void touchWaitingMeta(String userId, Duration ttl) {
        String metaKey = WAITING_META_PREFIX + userId;
        hashOperations.put(metaKey, "lastSeenAt", Instant.now().toString());
        if (ttl != null && !ttl.isZero() && !ttl.isNegative()) {
            stringRedisTemplate.expire(metaKey, ttl);
        }
    }

    public Long getWaitingRank(String userId) {
        return zSetOperations.rank(WAITING_QUEUE_KEY,
                (String) Objects.requireNonNull(userId, "userId must not be null"));
    }
}
