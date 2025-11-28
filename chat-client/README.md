# Node.js Chat Client

TypeScript chat client for the queue-server stack. It mirrors the existing Python client by calling `queue-api`, polling for tickets, and optionally connecting to the WebSocket-based chat server.

## Install

```bash
cd chat-client-node
npm install
```

## Usage

```bash
npm run cli -- \
  --queue-base-url http://localhost:8080 \
  --chat-base-url ws://localhost:8081 \
  --nickname demo-node
```

### Available options

| Option | Description |
| --- | --- |
| `--queue-base-url` | queue-api 베이스 URL (env `QUEUE_API_BASE_URL`, 기본 `http://localhost:8080`) |
| `--chat-base-url` | chat server URL (env `CHAT_SERVER_BASE_URL`, 기본 `ws://localhost:8081`) |
| `--nickname` | 사용할 닉네임 (기본: `ranger-<임의>`) |
| `--poll-interval` | 상태 폴링 간격(초, 기본 2) |
| `--max-polls` | 최대 폴링 횟수(기본 60) |
| `--skip-chat` | 티켓을 받은 후 채팅 연결을 생략 |
| `--message` | WebSocket 연결 직후 보내는 초기 메시지 |
| `--listen-seconds` | 지정 시간 후 자동 종료(0이면 `/quit`까지 유지) |

## Build / Run

```bash
npm run build
npm start -- --queue-base-url http://...
```
