package com.likelion.queuemanager.repository;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.likelion.queue.common.QueueRedisKeys;
import com.likelion.queue.common.TicketInfo;
import org.springframework.data.redis.core.HashOperations;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.ValueOperations;
import org.springframework.data.redis.core.ZSetOperations;
import org.springframework.stereotype.Repository;

import java.time.Duration;
import java.time.Instant;
import java.util.*;

@Repository
public class QueueManagerRedisRepository {

    private static final String WAITING_QUEUE_KEY = QueueRedisKeys.WAITING_QUEUE;
    private static final String WAITING_META_PREFIX = QueueRedisKeys.WAITING_META_PREFIX;
    private static final String USER_STATUS_PREFIX = QueueRedisKeys.USER_STATUS_PREFIX;
    private static final String GRANTED_TICKET_PREFIX = QueueRedisKeys.GRANTED_TICKET_PREFIX;
    private static final String SERVER_STATUS_KEY = QueueRedisKeys.SERVER_STATUS;

    private final ZSetOperations<String, String> zSetOperations;
    private final HashOperations<String, String, String> hashOperations;
    private final ValueOperations<String, String> valueOperations;
    private final ObjectMapper objectMapper;

    public QueueManagerRedisRepository(StringRedisTemplate stringRedisTemplate, ObjectMapper objectMapper) {
        this.zSetOperations = stringRedisTemplate.opsForZSet();
        this.hashOperations = stringRedisTemplate.opsForHash();
        this.valueOperations = stringRedisTemplate.opsForValue();
        this.objectMapper = objectMapper;
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
        Map<String, String> meta = hashOperations.entries(WAITING_META_PREFIX + userId);
        if (meta == null) {
            return Collections.emptyMap();
        }
        return meta;
    }

    public void updateUserStatus(String userId, String ticketId, Instant updatedAt) {
        Map<String, String> status = new HashMap<>();
        status.put("ticketId", ticketId);
        status.put("updatedAt", updatedAt.toString());
        hashOperations.putAll(USER_STATUS_PREFIX + userId, status);
    }

    public void storeTicket(String ticketId, TicketInfo payload, Duration ttl) {
        if (ttl == null || ttl.isNegative() || ttl.isZero()) {
            throw new IllegalArgumentException("Ticket TTL must be a positive duration");
        }
        try {
            String serialized = objectMapper.writeValueAsString(payload);
            valueOperations.set(GRANTED_TICKET_PREFIX + ticketId, serialized, ttl);
        } catch (JsonProcessingException e) {
            throw new IllegalStateException("Unable to serialize ticket payload", e);
        }
    }

    public Optional<TicketInfo> fetchTicket(String ticketId) {
        String payload = valueOperations.get(GRANTED_TICKET_PREFIX + ticketId);
        if (payload == null) {
            return Optional.empty();
        }
        try {
            return Optional.of(objectMapper.readValue(payload, TicketInfo.class));
        } catch (JsonProcessingException e) {
            throw new IllegalStateException("Unable to deserialize ticket payload", e);
        }
    }

    public Map<String, String> fetchServerStatus() {
        Map<String, String> status = hashOperations.entries(SERVER_STATUS_KEY);
        if (status == null) {
            return Collections.emptyMap();
        }
        return status;
    }
}
