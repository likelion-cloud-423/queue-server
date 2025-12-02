package com.likelion.queueapi.service;

import com.likelion.queueapi.config.QueueApiProperties;
import com.likelion.queueapi.dto.QueueEntryRequest;
import com.likelion.queueapi.dto.QueueEntryResponse;
import com.likelion.queueapi.dto.QueueStatusResponse;
import com.likelion.queueapi.model.QueueStatus;
import com.likelion.queueapi.repository.QueueRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.web.server.ResponseStatusException;

import java.time.Instant;
import java.util.Map;
import java.util.UUID;

@Service
public class QueueService {

    private static final Logger log = LoggerFactory.getLogger(QueueService.class);

    private final QueueRepository redisRepository;
    private final QueueApiProperties properties;
    private final MetricService metrics;

    public QueueService(QueueRepository redisRepository, QueueApiProperties properties, MetricService metrics) {
        this.redisRepository = redisRepository;
        this.properties = properties;
        this.metrics = metrics;
    }

    public QueueEntryResponse enqueue(QueueEntryRequest request) {
        metrics.recordEntryRequest();

        String userId = UUID.randomUUID().toString();
        long score = Instant.now().toEpochMilli();

        boolean added = redisRepository.addToWaitingQueue(userId, score);
        if (!added) {
            log.error("Failed to add user {} to waiting queue", userId);
            throw new ResponseStatusException(HttpStatus.INTERNAL_SERVER_ERROR, "Failed to register queue entry");
        }

        redisRepository.upsertWaitingMeta(userId, request.nickname(), properties.getWaitingMetaTtl());
        Long rank = redisRepository.getWaitingRank(userId);
        long normalizedRank = rank != null ? rank : 0L;

        return new QueueEntryResponse(QueueStatus.WAITING, normalizedRank, userId);
    }

    public QueueStatusResponse getStatus(String userId) {
        metrics.recordStatusRequest();

        Map<String, String> meta = redisRepository.findWaitingMeta(userId)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "User not found in queue"));

        redisRepository.touchWaitingMeta(userId, properties.getWaitingMetaTtl());

        String ticketId = meta.get("ticketId");
        if (ticketId != null && !ticketId.isBlank()) {
            metrics.recordPromotedUser();
            return new QueueStatusResponse(QueueStatus.PROMOTED, 0L, ticketId);
        }

        Long rank = redisRepository.getWaitingRank(userId);
        if (rank == null) {
            throw new ResponseStatusException(HttpStatus.GONE, "User is no longer waiting");
        }
        return new QueueStatusResponse(QueueStatus.WAITING, rank, null);
    }
}
