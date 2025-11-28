package com.likelion.queueapi;

import com.likelion.queueapi.config.QueueApiProperties;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.context.properties.EnableConfigurationProperties;

@EnableConfigurationProperties(QueueApiProperties.class)
@SpringBootApplication
public class QueueApiApplication {

    public static void main(String[] args) {
        SpringApplication.run(QueueApiApplication.class, args);
    }
}
