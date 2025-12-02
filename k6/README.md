# k6 Queue Server Load Test

Comprehensive load testing script for the queue-based chat system. Simulates multiple user behaviors from queue entry through chat participation to disconnection.

## Requirements

- [k6](https://k6.io/docs/getting-started/installation/) installed
- `queue-api` and `chat-server` running and accessible

## Quick Start

```bash
# Run with default URLs (localhost:8080, localhost:8081)
k6 run k6/chat-flow.js --vus 10 --duration 2m

# Run with custom endpoints
k6 run k6/chat-flow.js \
  --env QUEUE_URL=http://localhost:8080 \
  --env CHAT_URL=ws://localhost:8081
```

## Test Scenarios

The script includes 4 pre-configured scenarios that run sequentially:

| Scenario | Duration | Max VUs | Purpose |
|----------|----------|---------|---------|
| `smoke` | 1m | 5 | Basic functionality verification |
| `load` | 16m | 100 | Normal load with gradual ramp |
| `stress` | 10m | 300 | Push system limits |
| `spike` | 4m | 200 | Sudden traffic spike handling |

### Running Individual Scenarios

```bash
# Smoke test only
k6 run k6/chat-flow.js -s smoke

# Custom load test
k6 run k6/chat-flow.js \
  --vus 50 --duration 5m \
  --env BEHAVIOR_WEIGHTS="active_chatter:50,quick_visitor:30,idle_user:20"
```

## User Behaviors

Each virtual user randomly selects a behavior pattern:

| Behavior | Weight | Description |
|----------|--------|-------------|
| `active_chatter` | 40% | Normal user: chats periodically for ~30s |
| `quick_visitor` | 25% | Brief visit: sends 2 messages, leaves in ~5s |
| `idle_user` | 10% | Silent user: triggers idle timeout (~130s) |
| `burst_sender` | 15% | Rapid fire: sends 20 messages quickly |
| `long_session` | 5% | Marathon: stays connected ~2 minutes |
| `reconnector` | 5% | Unstable: connects briefly then disconnects |

### Custom Behavior Distribution

```bash
# Mostly active users
k6 run k6/chat-flow.js \
  --env BEHAVIOR_WEIGHTS="active_chatter:70,quick_visitor:20,idle_user:10"

# Test idle timeout handling
k6 run k6/chat-flow.js \
  --env BEHAVIOR_WEIGHTS="idle_user:100" \
  --env IDLE_TIMEOUT_WAIT_S=150

# Stress test with burst traffic
k6 run k6/chat-flow.js \
  --env BEHAVIOR_WEIGHTS="burst_sender:80,active_chatter:20" \
  --env BURST_MESSAGE_COUNT=50
```

## Environment Variables

### Connection Settings

| Variable | Default | Description |
|----------|---------|-------------|
| `QUEUE_URL` | `http://localhost:8080` | Queue API base URL |
| `CHAT_URL` | `ws://localhost:8081` | Chat server WebSocket URL |
| `POLL_INTERVAL_MS` | `2000` | Polling interval for ticket status |
| `POLL_LIMIT` | `120` | Max polling attempts before giving up |

### Behavior Settings

| Variable | Default | Description |
|----------|---------|-------------|
| `BEHAVIOR_WEIGHTS` | (see above) | Comma-separated `behavior:weight` pairs |
| `ACTIVE_CHAT_DURATION_S` | `30` | Active chatter session duration |
| `ACTIVE_MESSAGE_INTERVAL_S` | `3` | Interval between messages (active) |
| `QUICK_VISIT_DURATION_S` | `5` | Quick visitor session duration |
| `QUICK_VISIT_MESSAGES` | `2` | Messages sent by quick visitor |
| `IDLE_TIMEOUT_WAIT_S` | `130` | Idle user wait time |
| `BURST_MESSAGE_COUNT` | `20` | Messages in burst mode |
| `BURST_INTERVAL_MS` | `100` | Interval between burst messages |
| `LONG_SESSION_DURATION_S` | `120` | Long session duration |
| `LONG_SESSION_MESSAGE_INTERVAL_S` | `15` | Message interval (long session) |

### Logging

| Variable | Default | Description |
|----------|---------|-------------|
| `LOG_LEVEL` | `info` | `error`, `warn`, `info`, or `debug` |

## Metrics

### Custom Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `ticket_wait_ms` | Trend | Time from queue entry to ticket acquisition |
| `queue_entry_ms` | Trend | Queue entry API response time |
| `chat_connect_ms` | Trend | WebSocket connection time |
| `chat_session_duration_ms` | Trend | Total chat session duration |
| `queue_entry_success` | Rate | Queue entry success rate |
| `ticket_acquire_success` | Rate | Ticket acquisition success rate |
| `chat_connect_success` | Rate | WebSocket connection success rate |
| `chat_messages_sent` | Counter | Total messages sent |
| `chat_messages_received` | Counter | Total messages received |
| `chat_connections_total` | Counter | Total WebSocket connections |
| `chat_disconnects_total` | Counter | Total disconnections |
| `idle_timeouts_triggered` | Counter | Idle timeout disconnections |
| `graceful_exits` | Counter | Clean disconnections (code 1000) |
| `errors_total` | Counter | Total errors encountered |
| `active_connections` | Gauge | Current active connections |

### Thresholds

```javascript
thresholds: {
    "queue_entry_success": ["rate>0.95"],      // 95% success rate
    "ticket_acquire_success": ["rate>0.90"],   // 90% ticket acquisition
    "chat_connect_success": ["rate>0.90"],     // 90% chat connection
    "ticket_wait_ms": ["p(95)<60000"],         // 95th percentile under 60s
    "chat_connect_ms": ["p(95)<5000"],         // 95th percentile under 5s
    "errors_total": ["count<100"],             // Less than 100 errors
}
```

## Example Test Runs

### Development Testing

```bash
# Quick sanity check
k6 run k6/chat-flow.js --vus 5 --duration 30s --env LOG_LEVEL=debug
```

### Load Testing

```bash
# Simulate 100 concurrent users for 10 minutes
k6 run k6/chat-flow.js --vus 100 --duration 10m
```

### Stress Testing

```bash
# Push to 500 users
k6 run k6/chat-flow.js \
  --vus 500 --duration 15m \
  --env POLL_LIMIT=180 \
  --env BEHAVIOR_WEIGHTS="active_chatter:60,quick_visitor:30,burst_sender:10"
```

### Output to InfluxDB/Grafana

```bash
k6 run k6/chat-flow.js \
  --out influxdb=http://localhost:8086/k6 \
  --vus 100 --duration 10m
```

## Troubleshooting

### High ticket wait times

- Increase `POLL_LIMIT` for longer queues
- Check queue-manager's `ticket-ttl` and `batch-limit` settings

### WebSocket connection failures

- Verify chat-server is running
- Check firewall/network settings
- Ensure tickets haven't expired before connection

### Idle timeout not triggering

- Increase `IDLE_TIMEOUT_WAIT_S` beyond server's idle timeout setting
- Verify server's idle timeout configuration
