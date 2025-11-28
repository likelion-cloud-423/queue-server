package com.likelion.queueapi.dto;

import com.likelion.queueapi.model.QueueStatus;

public record QueueEntryResponse(QueueStatus status, long rank, String userId) {
}
