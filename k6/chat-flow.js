import http from "k6/http";
import ws from "k6/ws";
import { check, sleep, group } from "k6";
import { Counter, Trend, Rate, Gauge } from "k6/metrics";

// =============================================================================
// Custom Metrics
// =============================================================================
const ticketWaitTime = new Trend("ticket_wait_ms", true);
const queueEntryTime = new Trend("queue_entry_ms", true);
const chatConnectTime = new Trend("chat_connect_ms", true);
const chatSessionDuration = new Trend("chat_session_duration_ms", true);

const queueEntrySuccess = new Rate("queue_entry_success");
const ticketAcquireSuccess = new Rate("ticket_acquire_success");
const chatConnectSuccess = new Rate("chat_connect_success");

const chatMessagesSent = new Counter("chat_messages_sent");
const chatMessagesReceived = new Counter("chat_messages_received");
const chatConnectionsTotal = new Counter("chat_connections_total");
const chatDisconnectsTotal = new Counter("chat_disconnects_total");
const idleTimeoutsTriggered = new Counter("idle_timeouts_triggered");
const gracefulExits = new Counter("graceful_exits");
const errorCount = new Counter("errors_total");

const activeConnections = new Gauge("active_connections");

// =============================================================================
// User Behaviors - Simulates different user patterns
// =============================================================================
const UserBehavior = {
    // Normal active user: joins, chats for a while, leaves gracefully
    ACTIVE_CHATTER: "active_chatter",
    // Quick visitor: joins, sends few messages, leaves quickly
    QUICK_VISITOR: "quick_visitor",
    // Idle user: joins but never sends messages, triggers idle timeout
    IDLE_USER: "idle_user",
    // Burst sender: sends many messages rapidly
    BURST_SENDER: "burst_sender",
    // Long session: stays connected for extended period with occasional messages
    LONG_SESSION: "long_session",
    // Reconnector: connects, disconnects, and tries to reconnect
    RECONNECTOR: "reconnector",
};

// =============================================================================
// Configuration
// =============================================================================
const QUEUE_URL = __ENV.QUEUE_URL || "http://localhost:8080";
const CHAT_URL = __ENV.CHAT_URL || "ws://localhost:8081";

// Queue polling
const POLL_INTERVAL_MS = Number(__ENV.POLL_INTERVAL_MS) || 2000;
const POLL_LIMIT = Number(__ENV.POLL_LIMIT) || 120;

// Behavior weights (default distribution)
const BEHAVIOR_WEIGHTS = parseBehaviorWeights(__ENV.BEHAVIOR_WEIGHTS || "active_chatter:40,quick_visitor:25,idle_user:10,burst_sender:15,long_session:5,reconnector:5");

// Behavior-specific settings
const ACTIVE_CHAT_DURATION_S = Number(__ENV.ACTIVE_CHAT_DURATION_S) || 30;
const ACTIVE_MESSAGE_INTERVAL_S = Number(__ENV.ACTIVE_MESSAGE_INTERVAL_S) || 3;
const QUICK_VISIT_DURATION_S = Number(__ENV.QUICK_VISIT_DURATION_S) || 5;
const QUICK_VISIT_MESSAGES = Number(__ENV.QUICK_VISIT_MESSAGES) || 2;
const IDLE_TIMEOUT_WAIT_S = Number(__ENV.IDLE_TIMEOUT_WAIT_S) || 130;
const BURST_MESSAGE_COUNT = Number(__ENV.BURST_MESSAGE_COUNT) || 20;
const BURST_INTERVAL_MS = Number(__ENV.BURST_INTERVAL_MS) || 100;
const LONG_SESSION_DURATION_S = Number(__ENV.LONG_SESSION_DURATION_S) || 120;
const LONG_SESSION_MESSAGE_INTERVAL_S = Number(__ENV.LONG_SESSION_MESSAGE_INTERVAL_S) || 15;

// Logging
const LOG_LEVEL = resolveLogLevel(__ENV.LOG_LEVEL || "info");

// =============================================================================
// k6 Options - Load Test Scenarios
// =============================================================================
export const options = {
    scenarios: {
        // Smoke test - verify basic functionality
        smoke: {
            executor: "constant-vus",
            vus: 5,
            duration: "1m",
            tags: { scenario: "smoke" },
            env: { SCENARIO: "smoke" },
            startTime: "0s",
        },
        // Load test - gradual ramp up
        load: {
            executor: "ramping-vus",
            startVUs: 0,
            stages: [
                { duration: "2m", target: 50 },   // Ramp up to 50 users
                { duration: "5m", target: 50 },   // Stay at 50 users
                { duration: "2m", target: 100 },  // Ramp up to 100 users
                { duration: "5m", target: 100 },  // Stay at 100 users
                { duration: "2m", target: 0 },    // Ramp down
            ],
            tags: { scenario: "load" },
            env: { SCENARIO: "load" },
            startTime: "1m",
        },
        // Stress test - push limits
        stress: {
            executor: "ramping-vus",
            startVUs: 0,
            stages: [
                { duration: "2m", target: 100 },  // Ramp up
                { duration: "3m", target: 200 },  // Push higher
                { duration: "3m", target: 300 },  // Peak load
                { duration: "2m", target: 0 },    // Ramp down
            ],
            tags: { scenario: "stress" },
            env: { SCENARIO: "stress" },
            startTime: "18m",
        },
        // Spike test - sudden traffic spike
        spike: {
            executor: "ramping-vus",
            startVUs: 0,
            stages: [
                { duration: "30s", target: 10 },   // Normal load
                { duration: "30s", target: 200 },  // Spike!
                { duration: "1m", target: 200 },   // Stay at spike
                { duration: "30s", target: 10 },   // Back to normal
                { duration: "1m", target: 10 },    // Stay normal
            ],
            tags: { scenario: "spike" },
            env: { SCENARIO: "spike" },
            startTime: "30m",
        },
    },
    thresholds: {
        "queue_entry_success": ["rate>0.95"],
        "ticket_acquire_success": ["rate>0.80"],      // Relaxed for stress tests
        "chat_connect_success": ["rate>0.85"],        // Relaxed for stress tests
        "ticket_wait_ms": ["p(95)<300000"],           // 95% should get ticket within 5 minutes
        "chat_connect_ms": ["p(95)<10000"],           // 95% should connect within 10s
        "errors_total": ["count<500"],               // Allow more errors under heavy load
    },
};

// =============================================================================
// Main Test Function
// =============================================================================
export default function () {
    const userId = `k6-${__VU}-${__ITER}-${Date.now()}`;
    const nickname = `User_${__VU}_${__ITER}`;
    const behavior = selectBehavior();

    logInfo("main", `Starting iteration: user=${nickname}, behavior=${behavior}`);

    // Step 1: Enter the queue
    const entryResult = group("Queue Entry", () => enterQueue(userId, nickname));
    if (!entryResult.success) {
        errorCount.add(1);
        return;
    }

    // Step 2: Wait for ticket (poll or immediate)
    const ticketResult = group("Ticket Acquisition", () => 
        waitForTicket(entryResult.userId, entryResult.ticketId)
    );
    if (!ticketResult.success) {
        errorCount.add(1);
        return;
    }

    // Step 3: Connect to chat server and execute behavior
    group("Chat Session", () => 
        executeChatSession(ticketResult.ticketId, nickname, behavior)
    );
}

// =============================================================================
// Queue Entry
// =============================================================================
function enterQueue(userId, nickname) {
    const startTime = Date.now();
    
    const response = http.post(
        `${QUEUE_URL}/api/queue/entry`,
        JSON.stringify({ nickname }),
        {
            headers: { "Content-Type": "application/json" },
            tags: { name: "queue_entry" },
        }
    );

    queueEntryTime.add(Date.now() - startTime);

    const success = check(response, {
        "queue entry status 200": (r) => r.status === 200,
        "queue entry has userId": (r) => {
            const body = r.json();
            return body && (body.userId || body.user_id);
        },
    });

    queueEntrySuccess.add(success ? 1 : 0);

    if (!success) {
        logError("queue", `Entry failed: status=${response.status}, body=${response.body}`);
        return { success: false };
    }

    const data = response.json();
    return {
        success: true,
        userId: data.userId || data.user_id,
        ticketId: data.ticketId || data.ticket_id,  // May be null if queued
        status: data.status,
    };
}

// =============================================================================
// Ticket Acquisition (Polling)
// =============================================================================
function waitForTicket(userId, immediateTicketId) {
    const waitStart = Date.now();

    // If we got an immediate ticket, we're done
    if (immediateTicketId) {
        ticketWaitTime.add(0);
        ticketAcquireSuccess.add(1);
        logInfo("queue", `Immediate ticket: ${immediateTicketId}`);
        return { success: true, ticketId: immediateTicketId };
    }

    // Poll for ticket
    let ticketId = null;
    let polls = 0;
    const pollIntervalSeconds = POLL_INTERVAL_MS / 1000;

    while (!ticketId && polls < POLL_LIMIT) {
        sleep(pollIntervalSeconds);
        polls++;

        const response = http.get(
            `${QUEUE_URL}/api/queue/status?userId=${encodeURIComponent(userId)}`,
            { tags: { name: "queue_status" } }
        );

        if (response.status === 200) {
            const data = response.json();
            ticketId = data.ticketId || data.ticket_id;
            
            if (!ticketId) {
                logDebug("queue", `Poll #${polls}: status=${data.status}, rank=${data.rank || "N/A"}`);
            }
        } else {
            logError("queue", `Poll failed: status=${response.status}`);
        }
    }

    const waitDuration = Date.now() - waitStart;
    ticketWaitTime.add(waitDuration);

    if (ticketId) {
        ticketAcquireSuccess.add(1);
        logInfo("queue", `Ticket acquired after ${polls} polls (${waitDuration}ms): ${ticketId}`);
        return { success: true, ticketId };
    } else {
        ticketAcquireSuccess.add(0);
        logError("queue", `Failed to acquire ticket after ${polls} polls`);
        return { success: false };
    }
}

// =============================================================================
// Chat Session Execution
// =============================================================================
function executeChatSession(ticketId, nickname, behavior) {
    const wsUrl = `${CHAT_URL}/gameserver?ticketId=${encodeURIComponent(ticketId)}`;
    const connectStart = Date.now();
    let sessionStart = null;
    let connectionActive = false;

    const response = ws.connect(wsUrl, null, (socket) => {
        socket.on("open", () => {
            const connectDuration = Date.now() - connectStart;
            chatConnectTime.add(connectDuration);
            sessionStart = Date.now();
            connectionActive = true;
            
            chatConnectionsTotal.add(1);
            activeConnections.add(1);
            
            logInfo(behavior, `Connected (${connectDuration}ms)`);

            // Execute behavior-specific logic
            switch (behavior) {
                case UserBehavior.ACTIVE_CHATTER:
                    runActiveChatBehavior(socket, nickname);
                    break;
                case UserBehavior.QUICK_VISITOR:
                    runQuickVisitorBehavior(socket, nickname);
                    break;
                case UserBehavior.IDLE_USER:
                    runIdleUserBehavior(socket);
                    break;
                case UserBehavior.BURST_SENDER:
                    runBurstSenderBehavior(socket, nickname);
                    break;
                case UserBehavior.LONG_SESSION:
                    runLongSessionBehavior(socket, nickname);
                    break;
                case UserBehavior.RECONNECTOR:
                    runReconnectorBehavior(socket, nickname);
                    break;
                default:
                    runActiveChatBehavior(socket, nickname);
            }
        });

        socket.on("message", (data) => {
            chatMessagesReceived.add(1);
            logDebug(behavior, `Received: ${data.substring(0, 100)}...`);
        });

        socket.on("close", (code) => {
            if (connectionActive) {
                connectionActive = false;
                activeConnections.add(-1);
                chatDisconnectsTotal.add(1);
                
                if (sessionStart) {
                    chatSessionDuration.add(Date.now() - sessionStart);
                }
                
                logInfo(behavior, `Disconnected (code=${code})`);
                
                // Track specific disconnect reasons
                if (code === 1000) {
                    gracefulExits.add(1);
                } else if (code === 1006 || code === 4001 || code === 4002) {
                    // 1006: abnormal closure (server-side disconnect, idle timeout, etc.)
                    // 4001/4002: custom app-level timeout codes
                    idleTimeoutsTriggered.add(1);
                }
            }
        });

        socket.on("error", (error) => {
            // 1006 abnormal closure is expected when server closes connection
            const errorStr = String(error);
            if (errorStr.includes("1006") || errorStr.includes("unexpected EOF")) {
                logDebug(behavior, `Connection closed by server: ${error}`);
            } else {
                logError(behavior, `WebSocket error: ${error}`);
                errorCount.add(1);
            }
        });
    });

    const connectSuccess = check(response, {
        "websocket connected": (r) => r && r.status === 101,
    });

    chatConnectSuccess.add(connectSuccess ? 1 : 0);

    if (!connectSuccess) {
        logError(behavior, `WebSocket connection failed: status=${response?.status}`);
        errorCount.add(1);
    }
}

// =============================================================================
// Behavior Implementations
// =============================================================================

// Active Chatter: Regular user who chats periodically
function runActiveChatBehavior(socket, nickname) {
    const messageCount = Math.floor(ACTIVE_CHAT_DURATION_S / ACTIVE_MESSAGE_INTERVAL_S);
    let messageIndex = 0;
    let finished = false;

    sendMessage(socket, `Hello everyone! I'm ${nickname}`);

    socket.setInterval(() => {
        if (finished) return;
        messageIndex++;
        if (messageIndex <= messageCount) {
            const messages = [
                `Message #${messageIndex} from ${nickname}`,
                `Anyone there? - ${nickname}`,
                `Testing chat... ${messageIndex}`,
                `${nickname} is chatting!`,
            ];
            sendMessage(socket, messages[messageIndex % messages.length]);
        }
    }, ACTIVE_MESSAGE_INTERVAL_S * 1000);

    socket.setTimeout(() => {
        finished = true;
        sendMessage(socket, `Goodbye! - ${nickname}`);
        sleep(0.5);
        socket.close(1000);
    }, ACTIVE_CHAT_DURATION_S * 1000);
}

// Quick Visitor: Joins, sends few messages, leaves quickly
function runQuickVisitorBehavior(socket, nickname) {
    sendMessage(socket, `Hi! Quick visit from ${nickname}`);
    
    for (let i = 1; i <= QUICK_VISIT_MESSAGES; i++) {
        sleep(0.5);
        sendMessage(socket, `Quick msg ${i}/${QUICK_VISIT_MESSAGES}`);
    }

    socket.setTimeout(() => {
        sendMessage(socket, "Gotta go, bye!");
        sleep(0.3);
        socket.close(1000);
    }, QUICK_VISIT_DURATION_S * 1000);
}

// Idle User: Joins but never sends messages, waits for server timeout
function runIdleUserBehavior(socket) {
    logInfo("idle_user", `Staying idle for ${IDLE_TIMEOUT_WAIT_S}s to trigger timeout`);
    
    // Just wait - don't send any messages
    socket.setTimeout(() => {
        // If we reach here, idle timeout didn't kick in
        logInfo("idle_user", "Idle period complete, closing connection");
        socket.close(1000);
    }, IDLE_TIMEOUT_WAIT_S * 1000);
}

// Burst Sender: Sends many messages rapidly
function runBurstSenderBehavior(socket, nickname) {
    logInfo("burst_sender", `Sending ${BURST_MESSAGE_COUNT} messages with ${BURST_INTERVAL_MS}ms interval`);
    
    let sent = 0;
    let finished = false;
    
    socket.setInterval(() => {
        if (finished) return;
        sent++;
        if (sent <= BURST_MESSAGE_COUNT) {
            sendMessage(socket, `BURST[${sent}/${BURST_MESSAGE_COUNT}] from ${nickname}`);
        } else if (!finished) {
            finished = true;
            sendMessage(socket, `Burst complete! Sent ${BURST_MESSAGE_COUNT} messages`);
            socket.close(1000);
        }
    }, BURST_INTERVAL_MS);

    // Safety timeout
    socket.setTimeout(() => {
        if (!finished) {
            finished = true;
            socket.close(1000);
        }
    }, (BURST_MESSAGE_COUNT * BURST_INTERVAL_MS / 1000 + 10) * 1000);
}

// Long Session: Stays connected for extended period
function runLongSessionBehavior(socket, nickname) {
    logInfo("long_session", `Long session for ${LONG_SESSION_DURATION_S}s`);
    
    sendMessage(socket, `${nickname} is here for the long haul!`);
    
    let messageIndex = 0;
    let finished = false;
    
    socket.setInterval(() => {
        if (finished) return;
        messageIndex++;
        sendMessage(socket, `Still here... message #${messageIndex} from ${nickname}`);
    }, LONG_SESSION_MESSAGE_INTERVAL_S * 1000);

    socket.setTimeout(() => {
        finished = true;
        sendMessage(socket, `Long session complete after ${LONG_SESSION_DURATION_S}s. Bye!`);
        sleep(0.5);
        socket.close(1000);
    }, LONG_SESSION_DURATION_S * 1000);
}

// Reconnector: Connects briefly, disconnects, could reconnect
function runReconnectorBehavior(socket, nickname) {
    sendMessage(socket, `${nickname} connected (will disconnect soon)`);
    
    // Short session then disconnect
    socket.setTimeout(() => {
        sendMessage(socket, "Disconnecting...");
        sleep(0.2);
        socket.close(1000);
        // Note: Reconnection would require a new ticket, 
        // which is handled by the next iteration
    }, randomBetween(3, 8) * 1000);
}

// =============================================================================
// Helper Functions
// =============================================================================

function sendMessage(socket, message) {
    const payload = JSON.stringify({
        type: "MESSAGE_SEND",
        payload: { message },
    });
    socket.send(payload);
    chatMessagesSent.add(1);
    logDebug("chat", `Sent: ${message}`);
}

function selectBehavior() {
    const rand = Math.random() * 100;
    let cumulative = 0;
    
    for (const [behavior, weight] of Object.entries(BEHAVIOR_WEIGHTS)) {
        cumulative += weight;
        if (rand < cumulative) {
            return behavior;
        }
    }
    
    return UserBehavior.ACTIVE_CHATTER;
}

function parseBehaviorWeights(input) {
    const weights = {};
    const pairs = input.split(",");
    
    for (const pair of pairs) {
        const [name, weightStr] = pair.split(":").map(s => s.trim());
        const weight = Number(weightStr) || 10;
        
        // Normalize behavior name
        const normalizedName = name.toLowerCase().replace(/[^a-z_]/g, "");
        if (Object.values(UserBehavior).includes(normalizedName)) {
            weights[normalizedName] = weight;
        }
    }
    
    // Ensure we have at least one behavior
    if (Object.keys(weights).length === 0) {
        weights[UserBehavior.ACTIVE_CHATTER] = 100;
    }
    
    return weights;
}

function randomBetween(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

function resolveLogLevel(level) {
    const levels = { error: 0, warn: 1, info: 2, debug: 3 };
    return levels[level.toLowerCase()] ?? levels.info;
}

function logError(scope, message) {
    if (LOG_LEVEL >= 0) console.error(`[ERROR][${scope}] ${message}`);
}

function logWarn(scope, message) {
    if (LOG_LEVEL >= 1) console.warn(`[WARN][${scope}] ${message}`);
}

function logInfo(scope, message) {
    if (LOG_LEVEL >= 2) console.log(`[INFO][${scope}] ${message}`);
}

function logDebug(scope, message) {
    if (LOG_LEVEL >= 3) console.log(`[DEBUG][${scope}] ${message}`);
}

// =============================================================================
// Lifecycle Hooks
// =============================================================================

export function setup() {
    console.log("=".repeat(60));
    console.log("Queue Server Load Test");
    console.log("=".repeat(60));
    console.log(`Queue API: ${QUEUE_URL}`);
    console.log(`Chat Server: ${CHAT_URL}`);
    console.log(`Behavior Weights: ${JSON.stringify(BEHAVIOR_WEIGHTS)}`);
    console.log("=".repeat(60));
    
    // Verify endpoints are reachable
    const queueHealth = http.get(`${QUEUE_URL}/actuator/health`);
    if (queueHealth.status !== 200) {
        console.error(`WARNING: Queue API health check failed (status=${queueHealth.status})`);
    }
    
    return { startTime: Date.now() };
}

export function teardown(data) {
    const duration = (Date.now() - data.startTime) / 1000;
    console.log("=".repeat(60));
    console.log(`Load test completed in ${duration.toFixed(1)}s`);
    console.log("=".repeat(60));
}
