package com.likelion.queueapi.dto;

import jakarta.validation.constraints.NotBlank;

public record QueueEntryRequest(@NotBlank String nickname) {
}
