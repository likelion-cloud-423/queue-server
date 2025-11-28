# k6 Chat Flow Load Test

This script simulates users entering the queue, waiting for a ticket, and then opening a WebSocket chat session to exchange a couple of messages.

## Requirements

- [k6](https://k6.io/docs/getting-started/installation/) installed on your machine.
- `queue-api` and `chat-server` reachable from where you run the test.

## Running

```bash
k6 run load-tests/chat-flow.js --vus 20 --duration 1m \
  --env QUEUE_BASE_URL=http://localhost:8080 \
  --env CHAT_BASE_URL=ws://localhost:8081
```

### Environment variables

| Variable | Default | Description |
| --- | --- | --- |
| `QUEUE_BASE_URL` | `http://localhost:8080` | queue-api base URL |
| `CHAT_BASE_URL` | `ws://localhost:8081` | chat server URL |
| `POLL_INTERVAL_MS` | `2000` | poll interval between status checks |
| `POLL_LIMIT` | `60` | max polls before giving up |
| `CHAT_MESSAGES` | `2` | number of chat messages sent after promotion (active behavior) |
| `CHAT_DURATION_S` | `10` | seconds to keep the WebSocket open (active behavior) |
| `CHAT_INITIAL_MESSAGE` | `` | optional message to send immediately after connecting |
| `CHAT_MESSAGE_TEMPLATE` | `{nick} says hello (#{index})` | template for chat messages (supports `{nick}` and `{index}`) |
| `CHAT_BEHAVIORS` | `active,idle_timeout,random_burst` | comma-separated list (optionally `name:weight`) used to randomly pick a chat behavior per VU iteration; accepts `active`, `idle_timeout`, `random_burst` (legacy `CHAT_BEHAVIOR` still works for single choice) |
| `CHAT_IDLE_WAIT_S` | `130` | seconds to stay idle before closing when `CHAT_BEHAVIOR=idle_timeout` |
| `CHAT_RANDOM_MIN_S` | `5` | minimum lifetime (seconds) when `CHAT_BEHAVIOR=random_burst` |
| `CHAT_RANDOM_MAX_S` | `20` | maximum lifetime (seconds) when `CHAT_BEHAVIOR=random_burst` |
| `CHAT_RANDOM_INTERVAL_MS` | `800` | delay between random burst messages in `random_burst` mode |
| `CHAT_LOG_LEVEL` | `info` | `error`, `info`, or `debug`; controls how much detail the script logs |
| `CHAT_VERBOSE` | unset | set to any value to force debug logging (shortcut for `CHAT_LOG_LEVEL=debug`) |

### Chat behaviors

Each VU run randomly selects one behavior from `CHAT_BEHAVIORS` (respecting weights when provided).

- `active`: immediately exchange `CHAT_MESSAGES` messages, mirroring a normal chat user.
- `idle_timeout`: join the room and stay silent to trigger the server-side inactivity timeout (defaults to ~2 minutes).
- `random_burst`: send messages at random intervals for a random duration between `CHAT_RANDOM_MIN_S` and `CHAT_RANDOM_MAX_S`, then disconnect.

### Verbose logging & sample runs

Enable detailed logs (queue entry, poll attempts, WebSocket lifecycle, behavior actions) with:

```bash
k6 run load-tests/chat-flow.js \
  --vus 10 --duration 30s \
  --env CHAT_LOG_LEVEL=debug \
  --env CHAT_BEHAVIORS=active:3,idle_timeout:1,random_burst:1
```

If you only want to focus on a single scenario, override the behavior list:

```bash
# Idle users who never send chat messages (helpful for timeout testing)
k6 run load-tests/chat-flow.js \
  --vus 5 --duration 2m \
  --env CHAT_BEHAVIORS=idle_timeout \
  --env CHAT_IDLE_WAIT_S=150 \
  --env CHAT_LOG_LEVEL=debug
```

## Metrics exposed

- `ticket_wait_ms` — measured time from queue entry to ticket.
- `chat_success` / `chat_failure` — overall WebSocket success rate.
- `chat_messages_sent` / `chat_messages_received` — observed chat traffic.
- `http_req_failed` — fails if queue API is unavailable.

You can feed these metrics into Influx/Grafana or view k6's summary table after the test completes.
