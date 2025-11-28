package com.likelion.queuemanager.config;

import jakarta.validation.constraints.Positive;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.convert.DurationUnit;
import org.springframework.stereotype.Component;
import org.springframework.validation.annotation.Validated;

import java.time.Duration;
import java.time.temporal.ChronoUnit;

@Validated
@Component("queueManagerProperties")
@ConfigurationProperties(prefix = "queue.manager")
public class QueueManagerProperties {

    @DurationUnit(ChronoUnit.MILLIS)
    private Duration scheduleInterval = Duration.ofSeconds(1);

    @DurationUnit(ChronoUnit.SECONDS)
    private Duration ticketTtl = Duration.ofSeconds(60);

    @Positive
    private int batchLimit = 100;

    @Positive
    private long defaultSoftCap = 1000;

    @DurationUnit(ChronoUnit.SECONDS)
    private Duration inactivityGrace = Duration.ofSeconds(30);

    public long scheduleIntervalMillis() {
        long millis = scheduleInterval.toMillis();
        return millis > 0 ? millis : Duration.ofSeconds(1).toMillis();
    }

    public Duration getScheduleInterval() {
        return scheduleInterval;
    }

    public void setScheduleInterval(Duration scheduleInterval) {
        if (scheduleInterval != null && !scheduleInterval.isZero() && !scheduleInterval.isNegative()) {
            this.scheduleInterval = scheduleInterval;
        }
    }

    public Duration getTicketTtl() {
        return ticketTtl;
    }

    public void setTicketTtl(Duration ticketTtl) {
        if (ticketTtl != null && !ticketTtl.isZero() && !ticketTtl.isNegative()) {
            this.ticketTtl = ticketTtl;
        }
    }

    public int getBatchLimit() {
        return batchLimit;
    }

    public void setBatchLimit(int batchLimit) {
        if (batchLimit > 0) {
            this.batchLimit = batchLimit;
        }
    }

    public long getDefaultSoftCap() {
        return defaultSoftCap;
    }

    public void setDefaultSoftCap(long defaultSoftCap) {
        if (defaultSoftCap > 0) {
            this.defaultSoftCap = defaultSoftCap;
        }
    }

    public Duration getInactivityGrace() {
        return inactivityGrace;
    }

    public void setInactivityGrace(Duration inactivityGrace) {
        if (inactivityGrace != null && !inactivityGrace.isNegative() && !inactivityGrace.isZero()) {
            this.inactivityGrace = inactivityGrace;
        }
    }
}
