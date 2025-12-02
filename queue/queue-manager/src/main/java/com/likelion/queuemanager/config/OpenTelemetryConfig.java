package com.likelion.queuemanager.config;

import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.instrumentation.logback.appender.v1_0.OpenTelemetryAppender;
import io.opentelemetry.sdk.autoconfigure.AutoConfiguredOpenTelemetrySdk;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class OpenTelemetryConfig {

    @Bean
    public OpenTelemetry openTelemetry() {
        OpenTelemetry openTelemetry = AutoConfiguredOpenTelemetrySdk.initialize()
                .getOpenTelemetrySdk();
        OpenTelemetryAppender.install(openTelemetry);
        return openTelemetry;
    }
}
