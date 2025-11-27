# Chat Client (queue-api + chat-server)

Queue-API와 ChatServer(WebSocket) 간 전체 플로우를 빠르게 검증할 수 있는 파이썬 더미 클라이언트입니다. 대기열 진입/폴링 UX를 개선해 현재 상태를 시각적으로 보여주고, 티켓을 받으면 CLI 기반 실시간 채팅 프로그램처럼 자유롭게 메시지를 송수신할 수 있습니다.

## 요구 사항

- Python 3.11+
- [uv](https://github.com/astral-sh/uv) (의존성 설치 및 실행용)

```powershell
uv sync   # 가상환경 + 의존성 설치
uv run python -m chat_client.cli --help
```

## 빠른 사용 예시

```powershell
uv run chat-client `
  --queue-base-url http://localhost:8080 `
  --chat-base-url ws://localhost:5197 `
  --region ap-northeast-2 `
  --meta shard=alpha channel=1
```

### 동작 순서

1. `POST /api/queue/entry`
    - `userId`, `nickname`, `region`, `signature` 필드를 전송합니다.
    - 추가 메타데이터는 `--meta key=value` 형식으로 n개까지 전달 가능합니다.
2. `GET /api/queue/status?userId=...`
    - 시도 횟수, rank 변화, 티켓 여부를 즉시 출력하며 `PROMOTED/READY/GRANTED` 혹은 `ticketId`가 나올 때까지 반복합니다.
3. `GET /gameserver?ticketId=...` (WebSocket)
    - 접속 후 `/help`, `/quit` 명령을 사용할 수 있고, 프롬프트에서 바로 메시지를 입력해 송신/수신을 동시 처리합니다.

## 주요 옵션

| 옵션 | 설명 |
| --- | --- |
| `--queue-base-url` / `--chat-base-url` | Queue-API / ChatServer 베이스 URL |
| `--poll-interval` / `--max-polls` | 대기열 상태를 확인하는 간격과 최대 시도 횟수 |
| `--message` | 연결 직후 자동으로 보낼 초기 메시지 (기본값: 없음) |
| `--listen-seconds` | 지정한 시간이 지나면 세션을 종료(0이면 사용자가 `/quit` 입력 시까지) |
| `--skip-chat` | 티켓 발급만 확인하고 WebSocket 연결은 생략 |
| `--meta key=value ...` | Queue-API 요청에 포함할 임의의 메타데이터 쌍 |

## 동작 참고

- 모든 WebSocket 메시지는 `{"type": "...", "payload": { ... }}` 구조를 따릅니다. `type=chat`은 `timestamp/nickname/message`를 포함하고, `type=server_stats`는 `clientCount`를 반환합니다.
- `signature`는 `HMAC-SHA256(secret, userId)`로 계산합니다(`--signing-secret`).
- ChatServer 브로드캐스트 메시지를 그대로 출력하며, CLI 프롬프트에서 `/help`로 명령을 확인하고 `/quit`로 언제든 종료할 수 있습니다.
- Queue-API/Manager, ChatServer가 모두 기동 중이어야 end-to-end 시나리오가 성공합니다.
