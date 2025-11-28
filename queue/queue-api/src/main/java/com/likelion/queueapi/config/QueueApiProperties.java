package com.likelion.queueapi.config;

import jakarta.validation.constraints.NotNull;
import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.boot.convert.DurationUnit;
import org.springframework.validation.annotation.Validated;

import java.time.Duration;
import java.time.temporal.ChronoUnit;

@Validated
@ConfigurationProperties(prefix = "queue.api")
public class QueueApiProperties {

    @NotNull
    @DurationUnit(ChronoUnit.SECONDS)
    private Duration waitingMetaTtl = Duration.ofSeconds(30);

    public Duration getWaitingMetaTtl() {
        return waitingMetaTtl;
    }

    public void setWaitingMetaTtl(Duration waitingMetaTtl) {
        if (waitingMetaTtl != null && !waitingMetaTtl.isZero() && !waitingMetaTtl.isNegative()) {
            this.waitingMetaTtl = waitingMetaTtl;
        }
    }
}
