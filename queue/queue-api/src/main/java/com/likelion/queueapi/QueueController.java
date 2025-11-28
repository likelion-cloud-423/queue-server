package com.likelion.queueapi;

import com.likelion.queueapi.dto.QueueEntryRequest;
import com.likelion.queueapi.dto.QueueEntryResponse;
import com.likelion.queueapi.dto.QueueStatusResponse;
import com.likelion.queueapi.service.QueueService;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

@Validated
@RestController
@RequestMapping("/api/queue")
public class QueueController {

    private final QueueService queueService;

    public QueueController(QueueService queueService) {
        this.queueService = queueService;
    }

    @PostMapping("/entry")
    public QueueEntryResponse enterQueue(@Valid @RequestBody QueueEntryRequest request) {
        return queueService.enqueue(request);
    }

    @GetMapping("/status")
    public QueueStatusResponse getStatus(@RequestParam @NotBlank String userId) {
        return queueService.getStatus(userId);
    }
}
