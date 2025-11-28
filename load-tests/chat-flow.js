import http from "k6/http";
import ws from "k6/ws";
import { check, sleep } from "k6";
import { Counter, Trend } from "k6/metrics";

const ticketWait = new Trend("ticket_wait_ms");
const chatMessagesSent = new Counter("chat_messages_sent");
const chatMessagesReceived = new Counter("chat_messages_received");
const chatSuccess = new Counter("chat_success");
const chatFailure = new Counter("chat_failure");

const ChatBehaviors = {
    ACTIVE: "active",
    IDLE_TIMEOUT: "idle_timeout",
    RANDOM_BURST: "random_burst",
};

const LogLevels = {
    error: 0,
    info: 1,
    debug: 2,
};

const QUEUE_BASE = __ENV.QUEUE_BASE_URL || "http://localhost:8080";
const CHAT_BASE = __ENV.CHAT_BASE_URL || "ws://localhost:8081";
const POLL_INTERVAL_MS = Number(__ENV.POLL_INTERVAL_MS) || 2000;
const POLL_LIMIT = Number(__ENV.POLL_LIMIT) || 60;
const CHAT_MESSAGE_COUNT = Number(__ENV.CHAT_MESSAGES) || 2;
const CHAT_DURATION_SECONDS = Number(__ENV.CHAT_DURATION_S) || 10;
const CHAT_INITIAL_MESSAGE = (__ENV.CHAT_INITIAL_MESSAGE || "").trim();
const CHAT_MESSAGE_TEMPLATE = __ENV.CHAT_MESSAGE_TEMPLATE || "{nick} says hello (#{index})";
const CHAT_IDLE_WAIT_SECONDS = Number(__ENV.CHAT_IDLE_WAIT_S) || 130;
const CHAT_RANDOM_MIN_SECONDS = Math.max(1, Number(__ENV.CHAT_RANDOM_MIN_S) || 5);
const CHAT_RANDOM_MAX_SECONDS = Math.max(
    CHAT_RANDOM_MIN_SECONDS,
    Number(__ENV.CHAT_RANDOM_MAX_S) || 20,
);
const CHAT_RANDOM_INTERVAL_MS = Math.max(200, Number(__ENV.CHAT_RANDOM_INTERVAL_MS) || 800);
const CHAT_BEHAVIOR_POOL = buildBehaviorPool(
    __ENV.CHAT_BEHAVIORS || __ENV.CHAT_BEHAVIOR || "active,idle_timeout,random_burst",
);
const CHAT_LOG_LEVEL = resolveLogLevel(__ENV.CHAT_LOG_LEVEL, __ENV.CHAT_VERBOSE);

export const options = {
    thresholds: {
        http_req_failed: ["rate<0.1"],
        ticket_wait_ms: ["p(95)<15000"],
    },
};

export default function () {
    const nickname = `k6-${__VU}-${__ITER}-${Math.random().toString(36).slice(2, 6)}`;
    const behavior = pickBehavior();
    logDebug("behavior", `selected ${behavior}`);
    const entryRes = http.post(
        `${QUEUE_BASE}/api/queue/entry`,
        JSON.stringify({ nickname }),
        {
            headers: { "Content-Type": "application/json" },
        },
    );

    const entryOk = check(entryRes, {
        "queue entry succeeded": (r) => r.status === 200,
    });

    if (!entryOk) {
        logError("queue", `entry failed status=${entryRes.status}`);
        chatFailure.add(1);
        return;
    }

    const entryData = entryRes.json() || {};
    const userId = entryData.userId || entryData.user_id;
    if (!userId) {
        logError("queue", `missing userId in entry response: ${JSON.stringify(entryData)}`);
        chatFailure.add(1);
        return;
    }

    let ticketId = entryData.ticketId || entryData.ticket_id;
    const waitStart = Date.now();
    if (ticketId) {
        ticketWait.add(Date.now() - waitStart);
        logInfo("queue", `immediate promotion nickname=${nickname} userId=${userId}`);
    }
    const pollIntervalSeconds = Math.max(0.1, POLL_INTERVAL_MS / 1000);
    let polls = 0;

    while (!ticketId && polls < POLL_LIMIT) {
        const statusRes = http.get(`${QUEUE_BASE}/api/queue/status?userId=${encodeURIComponent(userId)}`);
        logInfo("queue", `polling status userId=${userId} attempt #${polls + 1}, status=${statusRes.status}`);
        if (statusRes.status === 200) {
            const statusData = statusRes.json() || {};
            ticketId = statusData.ticketId || statusData.ticket_id;
            logDebug(
                "queue",
                `poll #${polls + 1} status=${statusData.status || "-"} rank=${statusData.rank || "-"} ticket=${ticketId || "-"}`,
            );
        } else {
            logError("queue", `poll failed status=${statusRes.status}`);
        }
        polls += 1;
        if (ticketId) {
            ticketWait.add(Date.now() - waitStart);
            logInfo("queue", `ticket acquired userId=${userId} attempts=${polls}`);
            break;
        }
        sleep(pollIntervalSeconds);
    }

    if (!ticketId) {
        logError("queue", `ticket acquisition failed userId=${userId} polls=${polls}`);
        chatFailure.add(1);
        return;
    }

    const wsUrl = `${CHAT_BASE}/gameserver?ticketId=${encodeURIComponent(ticketId)}`;
    logInfo(behavior, `connecting ${wsUrl}`);

    const chatResponse = ws.connect(wsUrl, null, (socket) => {
        socket.on("open", () => {
            logInfo(behavior, `open ticket=${ticketId}`);
            if (behavior === ChatBehaviors.IDLE_TIMEOUT) {
                runIdleTimeoutChat(socket);
                return;
            }
            if (behavior === ChatBehaviors.RANDOM_BURST) {
                runRandomBurstChat(socket, nickname);
                return;
            }
            runActiveChat(socket, nickname);
        });

        socket.on("message", () => {
            chatMessagesReceived.add(1);
            logDebug(behavior, "message received");
        });

        socket.on("close", (code) => {
            logInfo(behavior, `close code=${code}`);
        });

        socket.on("error", (error) => {
            logError(behavior, `error: ${error}`);
            chatFailure.add(1);
        });
    });

    const wsOk = check(chatResponse, {
        "chat upgrade": (res) => res && res.status === 101,
    });

    if (wsOk) {
        chatSuccess.add(1);
    } else {
        const status = chatResponse && chatResponse.status;
        logError(behavior, `websocket upgrade failed status=${status}`);
        chatFailure.add(1);
    }
}

function buildMessage(message) {
    return JSON.stringify({
        type: "MESSAGE_SEND",
        payload: { message },
    });
}

function runActiveChat(socket, nickname) {
    if (CHAT_INITIAL_MESSAGE) {
        socket.send(buildMessage(CHAT_INITIAL_MESSAGE));
        chatMessagesSent.add(1);
        logDebug("active", `sent initial message "${CHAT_INITIAL_MESSAGE}"`);
    }

    for (let i = 0; i < CHAT_MESSAGE_COUNT; i += 1) {
        const message = buildTemplatedMessage(nickname, i + 1);
        socket.send(buildMessage(message));
        chatMessagesSent.add(1);
        logDebug("active", `sent message #${i + 1}`);
        sleep(0.3);
    }

    socket.setTimeout(() => {
        socket.close();
    }, CHAT_DURATION_SECONDS * 1000);
}

function runIdleTimeoutChat(socket) {
    logInfo("idle_timeout", `staying silent for ${CHAT_IDLE_WAIT_SECONDS}s`);
    socket.setTimeout(() => {
        socket.close();
    }, CHAT_IDLE_WAIT_SECONDS * 1000);
}

function runRandomBurstChat(socket, nickname) {
    if (CHAT_INITIAL_MESSAGE) {
        socket.send(buildMessage(CHAT_INITIAL_MESSAGE));
        chatMessagesSent.add(1);
        logDebug("random_burst", "sent initial message");
    }

    if (typeof socket.setInterval !== "function") {
        logInfo("random_burst", "socket.setInterval unavailable; falling back to active chat.");
        runActiveChat(socket, nickname);
        return;
    }

    const durationSeconds =
        Math.random() * (CHAT_RANDOM_MAX_SECONDS - CHAT_RANDOM_MIN_SECONDS) + CHAT_RANDOM_MIN_SECONDS;
    const stopAt = Date.now() + durationSeconds * 1000;
    let burstIndex = 1;

    const intervalId = socket.setInterval(() => {
        if (Date.now() >= stopAt) {
            socket.close();
            return;
        }
        const message = buildTemplatedMessage(nickname, burstIndex);
        burstIndex += 1;
        socket.send(buildMessage(message));
        chatMessagesSent.add(1);
        logDebug("random_burst", `sent burst message #${burstIndex - 1}`);
    }, CHAT_RANDOM_INTERVAL_MS);

    socket.on("close", () => {
        if (intervalId && typeof socket.clearInterval === "function") {
            socket.clearInterval(intervalId);
        }
    });

    logInfo(
        "random_burst",
        `chatting for ~${durationSeconds.toFixed(1)}s (interval ${CHAT_RANDOM_INTERVAL_MS}ms)`,
    );
    socket.setTimeout(() => {
        socket.close();
    }, (durationSeconds + 5) * 1000);
}

function buildTemplatedMessage(nickname, index) {
    return CHAT_MESSAGE_TEMPLATE.replace("{nick}", nickname).replace("{index}", String(index));
}

function resolveChatBehavior(raw) {
    switch (raw) {
        case ChatBehaviors.IDLE_TIMEOUT:
        case "idle":
        case "idletimeout":
            return ChatBehaviors.IDLE_TIMEOUT;
        case ChatBehaviors.RANDOM_BURST:
        case "random":
        case "burst":
            return ChatBehaviors.RANDOM_BURST;
        case ChatBehaviors.ACTIVE:
        default:
            return ChatBehaviors.ACTIVE;
    }
}

function buildBehaviorPool(input) {
    const tokens = String(input)
        .split(",")
        .map((item) => item.trim())
        .filter(Boolean);

    const expanded = [];

    tokens.forEach((token) => {
        const parts = token.split(":").map((part) => part.trim());
        const name = parts[0] || "";
        const weightRaw = parts[1];
        const behavior = resolveChatBehavior(name.toLowerCase());
        const weight = Math.max(1, Number(weightRaw) || 1);
        for (let i = 0; i < weight; i += 1) {
            expanded.push(behavior);
        }
    });

    if (expanded.length === 0) {
        expanded.push(ChatBehaviors.ACTIVE);
    }

    return expanded;
}

function pickBehavior() {
    const index = Math.floor(Math.random() * CHAT_BEHAVIOR_POOL.length);
    return CHAT_BEHAVIOR_POOL[index] || ChatBehaviors.ACTIVE;
}

function resolveLogLevel(rawLevel, verboseFlag) {
    const normalized = (rawLevel || "").toLowerCase();
    if (normalized in LogLevels) {
        return LogLevels[normalized];
    }
    if (String(verboseFlag || "").trim() !== "") {
        return LogLevels.debug;
    }
    return LogLevels.info;
}

function logInfo(scope, message) {
    logWithLevel("info", scope, message);
}

function logDebug(scope, message) {
    logWithLevel("debug", scope, message);
}

function logError(scope, message) {
    logWithLevel("error", scope, message);
}

function logWithLevel(level, scope, message) {
    if (LogLevels[level] > CHAT_LOG_LEVEL) {
        return;
    }
    const prefix = scope ? `[chat][${scope}]` : "[chat]";
    if (level === "error") {
        console.error(`${prefix} ${message}`);
    } else {
        console.log(`${prefix} ${message}`);
    }
}
