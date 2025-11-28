package com.likelion.queuemanager.model;

public record ServerStatus(long currentUsers, Long softCap, Long maxCap) {

    public long resolveSoftCap(long fallback) {
        if (softCap != null && softCap > 0) {
            return softCap;
        }
        if (maxCap != null && maxCap > 0) {
            return maxCap;
        }
        return fallback;
    }
}
