package com.likelion.queuemanager.service;

import com.likelion.queuemanager.repository.QueueManagerRepository;
import io.micrometer.core.instrument.Gauge;
import io.micrometer.core.instrument.MeterRegistry;
import org.springframework.stereotype.Component;

import java.util.concurrent.atomic.AtomicLong;

/**
 * Queue Manager 커스텀 메트릭
 * 대기열 상태를 실시간으로 모니터링하기 위한 게이지 메트릭
 */
@Component
public class MetricService {

    private final QueueManagerRepository repository;

    private final AtomicLong waitingUsers = new AtomicLong(0);
    private final AtomicLong joiningUsers = new AtomicLong(0);
    private final AtomicLong currentUsers = new AtomicLong(0);
    private final AtomicLong softCap = new AtomicLong(0);
    private final AtomicLong availableSlots = new AtomicLong(0);

    public MetricService(MeterRegistry meterRegistry, QueueManagerRepository repository) {
        this.repository = repository;

        Gauge.builder("queue.waiting_users", waitingUsers, AtomicLong::get)
                .description("Number of users waiting in queue")
                .register(meterRegistry);

        Gauge.builder("queue.joining_users", joiningUsers, AtomicLong::get)
                .description("Number of users with tickets waiting to join game server")
                .register(meterRegistry);

        Gauge.builder("queue.current_users", currentUsers, AtomicLong::get)
                .description("Number of users currently connected to game server")
                .register(meterRegistry);

        Gauge.builder("queue.soft_cap", softCap, AtomicLong::get)
                .description("Current soft cap for game server connections")
                .register(meterRegistry);

        Gauge.builder("queue.available_slots", availableSlots, AtomicLong::get)
                .description("Number of available slots for new connections")
                .register(meterRegistry);
    }

    /**
     * 대기열 상태 메트릭 업데이트
     */
    public void updateQueueMetrics(long waitingCount, long joiningCount, long currentCount, long softCapValue) {
        this.waitingUsers.set(waitingCount);
        this.joiningUsers.set(joiningCount);
        this.currentUsers.set(currentCount);
        this.softCap.set(softCapValue);

        long slots = Math.max(0, softCapValue - (currentCount + joiningCount));
        this.availableSlots.set(slots);
    }

    /**
     * Repository에서 직접 대기열 크기 조회하여 업데이트
     */
    public void refreshWaitingCount() {
        Long size = repository.waitingSize();
        if (size != null) {
            this.waitingUsers.set(size);
        }
    }
}
