package com.likelion.queueapi.dto;

import com.likelion.queueapi.model.QueueStatus;

public record QueueStatusResponse(QueueStatus status, Long rank, String ticketId) {
}
