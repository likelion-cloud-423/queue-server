package com.likelion.queueapi.service;

import io.micrometer.core.instrument.Counter;
import io.micrometer.core.instrument.MeterRegistry;
import org.springframework.stereotype.Component;

/**
 * Queue API 커스텀 메트릭
 */
@Component
public class MetricService {

    private final Counter entryRequestsTotal;
    private final Counter statusRequestsTotal;
    private final Counter promotedUsersTotal;

    public MetricService(MeterRegistry meterRegistry) {
        this.entryRequestsTotal = Counter.builder("queue.entry_requests_total")
                .description("Total number of queue entry requests")
                .register(meterRegistry);

        this.statusRequestsTotal = Counter.builder("queue.status_requests_total")
                .description("Total number of queue status requests")
                .register(meterRegistry);

        this.promotedUsersTotal = Counter.builder("queue.promoted_users_total")
                .description("Total number of users promoted (received ticket)")
                .register(meterRegistry);
    }

    public void recordEntryRequest() {
        entryRequestsTotal.increment();
    }

    public void recordStatusRequest() {
        statusRequestsTotal.increment();
    }

    public void recordPromotedUser() {
        promotedUsersTotal.increment();
    }
}
