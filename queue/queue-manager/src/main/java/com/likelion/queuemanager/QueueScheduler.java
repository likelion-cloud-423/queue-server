package com.likelion.queuemanager;

import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

@Component
public class QueueScheduler {

    @Scheduled(fixedDelay = 1000)
    public void processQueue() {
        // 큐 처리 로직 구현
        System.out.println("Processing queue...");
    }
}
