package com.likelion.queueapi.repository;

import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.likelion.queueapi.dto.UserQueueStatus;
import com.likelion.queue.common.QueueRedisKeys;
import com.likelion.queue.common.TicketInfo;
import org.springframework.data.redis.core.HashOperations;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.ValueOperations;
import org.springframework.data.redis.core.ZSetOperations;
import org.springframework.stereotype.Repository;

import java.util.*;

@Repository
public class QueueRedisRepository {

    private final ZSetOperations<String, String> zSetOperations;
    private final HashOperations<String, String, String> hashOperations;
    private final ValueOperations<String, String> valueOperations;
    private final ObjectMapper objectMapper;

    public QueueRedisRepository(StringRedisTemplate stringRedisTemplate, ObjectMapper objectMapper) {
        this.zSetOperations = stringRedisTemplate.opsForZSet();
        this.hashOperations = stringRedisTemplate.opsForHash();
        this.valueOperations = stringRedisTemplate.opsForValue();
        this.objectMapper = objectMapper;
    }

    public Long getWaitingRank(String userId) {
        return zSetOperations.rank(QueueRedisKeys.WAITING_QUEUE, userId);
    }

    public Long getWaitingSize() {
        return zSetOperations.zCard(QueueRedisKeys.WAITING_QUEUE);
    }

    public List<String> getHeadOfQueue(int limit) {
        if (limit <= 0) {
            return Collections.emptyList();
        }
        Set<String> range = zSetOperations.range(QueueRedisKeys.WAITING_QUEUE, 0, limit - 1);
        if (range == null || range.isEmpty()) {
            return Collections.emptyList();
        }
        return new ArrayList<>(range);
    }

    public Optional<UserQueueStatus> findUserStatus(String userId) {
        Map<String, String> entries = hashOperations.entries(QueueRedisKeys.USER_STATUS_PREFIX + userId);
        if (entries == null || entries.isEmpty()) {
            return Optional.empty();
        }
        return Optional.of(new UserQueueStatus(entries.get("ticketId"), entries.get("updatedAt")));
    }

    public Optional<String> findTicketPayload(String ticketId) {
        return Optional.ofNullable(valueOperations.get(QueueRedisKeys.GRANTED_TICKET_PREFIX + ticketId));
    }

    public Optional<TicketInfo> findTicket(String ticketId) {
        return findTicketPayload(ticketId).map(payload -> {
            try {
                return objectMapper.readValue(payload, TicketInfo.class);
            } catch (JsonProcessingException e) {
                throw new IllegalStateException("Unable to deserialize ticket payload", e);
            }
        });
    }
}
