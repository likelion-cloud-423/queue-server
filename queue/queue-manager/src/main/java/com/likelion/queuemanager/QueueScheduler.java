package com.likelion.queuemanager;

import com.likelion.queuemanager.config.QueueManagerProperties;
import com.likelion.queuemanager.model.ServerStatus;
import com.likelion.queuemanager.repository.QueueManagerRepository;
import io.micrometer.core.instrument.Counter;
import io.micrometer.core.instrument.MeterRegistry;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

import java.time.Duration;
import java.time.Instant;
import java.time.format.DateTimeParseException;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

@Component
public class QueueScheduler {

    private static final Logger log = LoggerFactory.getLogger(QueueScheduler.class);

    private final QueueManagerRepository redisRepository;
    private final QueueManagerProperties properties;
    private final Counter issuedCounter;
    private final Counter expiredCounter;

    public QueueScheduler(QueueManagerRepository redisRepository,
                          QueueManagerProperties properties,
                          MeterRegistry meterRegistry) {
        this.redisRepository = redisRepository;
        this.properties = properties;
        this.issuedCounter = meterRegistry.counter("ticket_issued_count");
        this.expiredCounter = meterRegistry.counter("ticket_expired_count");
    }

    @Scheduled(fixedDelayString = "#{@queueManagerProperties.scheduleIntervalMillis()}")
    public void processQueue() {
        try {
            long now = System.currentTimeMillis();
            handleExpiredTickets(now);
            scheduleNextBatch(now);
        } catch (Exception ex) {
            log.error("Queue scheduling cycle failed", ex);
        }
    }

    private void handleExpiredTickets(long nowEpochMillis) {
        Set<String> expiredIds = redisRepository.removeExpiredTickets(nowEpochMillis);
        if (expiredIds.isEmpty()) {
            return;
        }
        redisRepository.deleteTicketHashes(expiredIds);
        expiredCounter.increment(expiredIds.size());
        if (log.isDebugEnabled()) {
            log.debug("Cleaned up {} expired tickets", expiredIds.size());
        }
    }

    private void scheduleNextBatch(long nowEpochMillis) {
        ServerStatus serverStatus = redisRepository.fetchServerStatus();
        long joiningUsers = redisRepository.countJoiningTickets(nowEpochMillis);
        long currentUsers = serverStatus.currentUsers();
        long softCap = serverStatus.resolveSoftCap(properties.getDefaultSoftCap());
        long availableSlots = softCap - (currentUsers + joiningUsers);
        if (availableSlots <= 0) {
            if (log.isDebugEnabled()) {
                log.debug("No capacity available (softCap={}, current={}, joining={})",
                        softCap, currentUsers, joiningUsers);
            }
            return;
        }

        int batchSize = (int) Math.min(availableSlots, properties.getBatchLimit());
        if (batchSize <= 0) {
            return;
        }

        List<String> candidates = redisRepository.fetchNextBatch(batchSize);
        if (candidates.isEmpty()) {
            return;
        }

        Duration ticketTtl = properties.getTicketTtl();
        if (ticketTtl == null || ticketTtl.isZero() || ticketTtl.isNegative()) {
            log.warn("Ticket TTL is not configured properly, skipping scheduling cycle");
            return;
        }
        int issuedThisCycle = 0;
        for (String userId : candidates) {
            Map<String, String> meta = redisRepository.fetchUserMeta(userId);
            if (meta == null || meta.isEmpty()) {
                dropWaitingUser(userId);
                continue;
            }

            if (isInactive(meta, nowEpochMillis)) {
                dropWaitingUser(userId);
                if (log.isDebugEnabled()) {
                    log.debug("Removed inactive user {} from waiting queue", userId);
                }
                continue;
            }

            String nickname = meta.get("nickname");
            if (nickname == null || nickname.isBlank()) {
                dropWaitingUser(userId);
                continue;
            }

            String ticketId = UUID.randomUUID().toString();
            Instant expireAt = Instant.now().plus(ticketTtl);
            boolean promoted = redisRepository.promoteToJoining(userId, ticketId, expireAt, ticketTtl);
            if (promoted) {
                issuedThisCycle++;
            }
        }

        if (issuedThisCycle > 0) {
            issuedCounter.increment(issuedThisCycle);
            log.info("Issued {} tickets (softCap={}, current={}, joining={})",
                    issuedThisCycle, softCap, currentUsers, joiningUsers);
        }
    }

    private void dropWaitingUser(String userId) {
        redisRepository.removeFromWaiting(userId);
        redisRepository.deleteWaitingMeta(userId);
    }

    private boolean isInactive(Map<String, String> meta, long nowEpochMillis) {
        Duration grace = properties.getInactivityGrace();
        if (grace == null || grace.isZero() || grace.isNegative()) {
            return false;
        }
        String lastSeenRaw = meta.get("lastSeenAt");
        if (lastSeenRaw == null || lastSeenRaw.isBlank()) {
            return false;
        }
        try {
            Instant lastSeen = Instant.parse(lastSeenRaw);
            long inactiveMillis = nowEpochMillis - lastSeen.toEpochMilli();
            return inactiveMillis > grace.toMillis();
        } catch (DateTimeParseException ex) {
            if (log.isDebugEnabled()) {
                log.debug("Failed to parse lastSeenAt={} for userId={}", lastSeenRaw, meta.get("userId"));
            }
            return false;
        }
    }
}
