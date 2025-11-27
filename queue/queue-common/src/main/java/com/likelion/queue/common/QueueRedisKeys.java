package com.likelion.queue.common;

public final class QueueRedisKeys {

    public static final String WAITING_QUEUE = "queue:waiting";
    public static final String WAITING_META_PREFIX = "queue:waiting:meta:";
    public static final String USER_STATUS_PREFIX = "queue:user-status:";
    public static final String GRANTED_TICKET_PREFIX = "queue:granted:";
    public static final String SERVER_STATUS = "server:status";

    private QueueRedisKeys() {
    }
}
