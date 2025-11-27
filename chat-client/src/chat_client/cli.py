import argparse
import asyncio
import hashlib
import hmac
import json
import os
import random
import string
import sys
import time
from dataclasses import dataclass
from typing import Any, Dict, Optional

import httpx
import websockets

from .messages import MessageSend, ServerStatusRequest, WebSocketMessageTypes


DEFAULT_QUEUE_BASE = os.environ.get("QUEUE_API_BASE_URL", "http://localhost:8080")
DEFAULT_CHAT_BASE = os.environ.get("CHAT_SERVER_BASE_URL", "ws://localhost:8081")


@dataclass(slots=True)
class ClientConfig:
    queue_base_url: str
    chat_base_url: str
    user_id: str
    nickname: str
    region: str
    signature: str
    meta: Dict[str, str]
    poll_interval: float
    max_polls: int
    skip_chat: bool
    initial_message: str
    listen_seconds: int

    @classmethod
    def from_namespace(cls, args: argparse.Namespace) -> "ClientConfig":
        user_id = args.user_id or _random_user_id()
        nickname = args.nickname or f"ranger-{user_id[-4:]}"
        signature = _build_signature(user_id, args.signing_secret)

        return cls(
            queue_base_url=args.queue_base_url.rstrip("/"),
            chat_base_url=args.chat_base_url.rstrip("/"),
            user_id=user_id,
            nickname=nickname,
            region=args.region,
            signature=signature,
            meta=_parse_meta(args.meta),
            poll_interval=args.poll_interval,
            max_polls=args.max_polls,
            skip_chat=args.skip_chat,
            initial_message=args.message.strip(),
            listen_seconds=args.listen_seconds,
        )


def run_cli() -> None:
    """Entry point used by `python -m chat_client.cli` and the console script."""
    args = _parse_args()
    try:
        asyncio.run(main(args))
    except KeyboardInterrupt:
        print("사용자에 의해 중단되었습니다.", file=sys.stderr)


async def main(args: argparse.Namespace) -> None:
    """Queue API ↔ ChatServer 연동 플로우."""
    config = ClientConfig.from_namespace(args)
    _print_banner(config)

    async with httpx.AsyncClient(timeout=httpx.Timeout(10.0, read=30.0)) as http_client:
        queue_client = QueueApiClient(config.queue_base_url, http_client)
        ticket_id = await _acquire_ticket(queue_client, config)

    if not ticket_id:
        print(
            "티켓을 받지 못했습니다. Queue-Manager가 동작 중인지 확인하세요.",
            file=sys.stderr,
        )
        return

    if config.skip_chat:
        print("--skip-chat 옵션이 설정되어 ChatServer 연결을 생략합니다.")
        return

    chat_client = ChatServerClient(config.chat_base_url)
    await chat_client.play_session(
        ticket_id=ticket_id,
        nickname=config.nickname,
        outbound_message=config.initial_message,
        listen_seconds=config.listen_seconds,
    )


def _print_banner(config: ClientConfig) -> None:
    print("=" * 60)
    print("Queue Chat Client")
    print(f" - queue-api : {config.queue_base_url}")
    print(f" - ChatServer: {config.chat_base_url}")
    print(f" - User      : {config.user_id} ({config.nickname})")
    print("=" * 60)


async def _acquire_ticket(
    queue_client: "QueueApiClient", config: ClientConfig
) -> Optional[str]:
    print(f"[queue-api] /api/queue/entry 호출 중... userId={config.user_id}")
    entry_data = await queue_client.enter_queue(
        user_id=config.user_id,
        nickname=config.nickname,
        region=config.region,
        signature=config.signature,
        meta=config.meta,
    )

    status = QueueStatus.from_dict(entry_data)
    _render_status("entry 응답", status, entry_data)

    ticket_id = status.ticket_id
    if ticket_id:
        return ticket_id

    return await _wait_for_ticket(
        queue_client=queue_client,
        user_id=config.user_id,
        poll_interval=config.poll_interval,
        max_attempts=config.max_polls,
    )


async def _wait_for_ticket(
    queue_client: "QueueApiClient",
    user_id: str,
    poll_interval: float,
    max_attempts: int,
) -> Optional[str]:
    """주기적으로 /api/queue/status를 조회하여 티켓을 기다린다."""
    display = QueueWaitDisplay()
    for attempt in range(1, max_attempts + 1):
        try:
            response = await queue_client.poll_status(user_id=user_id)
        except httpx.HTTPError as exc:
            print(
                f"[queue-api] 상태 조회 실패 ({exc}); {poll_interval}s 대기 후 재시도",
                file=sys.stderr,
            )
            await asyncio.sleep(poll_interval)
            continue

        status = QueueStatus.from_dict(response)
        display.emit(status, attempt=attempt, max_attempts=max_attempts)

        if status.is_promoted:
            return status.ticket_id

        await asyncio.sleep(poll_interval)

    return None


def _render_status(prefix: str, status: "QueueStatus", raw: Dict[str, Any]) -> None:
    rank = "미확인" if status.rank is None else status.rank
    ticket = status.ticket_id or "-"
    print(f"[{prefix}] status={status.status} rank={rank} ticketId={ticket}")
    if raw:
        print(f"  raw: {json.dumps(raw, ensure_ascii=False)}")


def _build_signature(user_id: str, secret: str) -> str:
    """Queue API 요청에 포함할 HMAC-SHA256 signature."""
    return hmac.new(
        secret.encode("utf-8"), user_id.encode("utf-8"), hashlib.sha256
    ).hexdigest()


def _random_user_id() -> str:
    suffix = "".join(random.choices(string.ascii_lowercase + string.digits, k=6))
    return f"user-{suffix}"


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="queue-api ↔ ChatServer 연동용 더미 클라이언트"
    )
    parser.add_argument(
        "--queue-base-url",
        default=DEFAULT_QUEUE_BASE,
        help="queue-api 베이스 URL (기본: %(default)s)",
    )
    parser.add_argument(
        "--chat-base-url",
        default=DEFAULT_CHAT_BASE,
        help="ChatServer 베이스 URL (기본: %(default)s)",
    )
    parser.add_argument("--user-id", help="명시적 userId (미지정 시 랜덤)")
    parser.add_argument("--nickname", help="채팅에 사용할 닉네임 (기본: userId 기반)")
    parser.add_argument(
        "--region", default="global", help="queue-api 요청 region 필드 값"
    )
    parser.add_argument(
        "--signing-secret",
        default="local-dev-secret",
        help="signature 계산 시 사용할 비밀 값",
    )
    parser.add_argument(
        "--meta", nargs="*", default=[], help="추가 메타데이터 (key=value 형태)"
    )
    parser.add_argument(
        "--poll-interval", type=float, default=2.0, help="상태 폴링 간격 (초)"
    )
    parser.add_argument("--max-polls", type=int, default=60, help="최대 폴링 횟수")
    parser.add_argument(
        "--skip-chat",
        action="store_true",
        help="티켓 발급까지만 수행하고 ChatServer 연결은 생략",
    )
    parser.add_argument(
        "--message",
        default="",
        help="연결 직후 자동으로 전송할 메시지 (기본: 전송 안 함)",
    )
    parser.add_argument(
        "--listen-seconds",
        type=int,
        default=0,
        help="세션을 강제로 종료할 시간(초). 0이면 사용자가 /quit 입력 시까지 유지",
    )
    return parser.parse_args()


@dataclass
class QueueStatus:
    status: str
    rank: Optional[int]
    ticket_id: Optional[str]

    @property
    def is_promoted(self) -> bool:
        return bool(self.ticket_id) or self.status in {"PROMOTED", "READY", "GRANTED"}

    @classmethod
    def from_dict(cls, data: Optional[Dict[str, Any]]) -> "QueueStatus":
        data = data or {}
        status = str(data.get("status", "UNKNOWN")).upper()
        rank = data.get("rank")
        try:
            rank = int(rank) if rank is not None else None
        except (TypeError, ValueError):
            rank = None
        ticket_id = data.get("ticketId") or data.get("ticket_id")
        return cls(status=status, rank=rank, ticket_id=ticket_id)


class QueueWaitDisplay:
    """대기열 상태 변화를 사용자 친화적으로 출력."""

    def __init__(self) -> None:
        self._start = time.time()
        self._last_rank: Optional[int] = None
        self._last_status: Optional[str] = None

    def emit(self, status: QueueStatus, attempt: int, max_attempts: int) -> None:
        elapsed = int(time.time() - self._start)
        rank_txt = "미확인" if status.rank is None else str(status.rank)
        movement = ""
        if self._last_rank is not None and status.rank is not None:
            delta = self._last_rank - status.rank
            if delta > 0:
                movement = f" (앞으로 {delta}칸 진입)"
            elif delta < 0:
                movement = f" (뒤로 {abs(delta)}칸 밀림)"

        ticket_txt = f", ticketId={status.ticket_id}" if status.ticket_id else ""
        print(
            f"[queue] t+{elapsed:02d}s / 시도 {attempt}/{max_attempts} "
            f"=> status={status.status} rank={rank_txt}{movement}{ticket_txt}"
        )

        self._last_rank = status.rank
        self._last_status = status.status


class QueueApiClient:
    """queue-api 엔드포인트와 상호작용을 담당."""

    def __init__(self, base_url: str, http_client: httpx.AsyncClient) -> None:
        self.base_url = base_url.rstrip("/")
        self._http = http_client

    async def enter_queue(
        self,
        user_id: str,
        nickname: str,
        region: str,
        signature: str,
        meta: Dict[str, str],
    ) -> Dict[str, Any]:
        payload: Dict[str, Any] = {
            "userId": user_id,
            "nickname": nickname,
            "region": region,
            "signature": signature,
        }
        if meta:
            payload["meta"] = meta

        response = await self._http.post(
            f"{self.base_url}/api/queue/entry", json=payload
        )
        response.raise_for_status()
        return response.json()

    async def poll_status(self, user_id: str) -> Dict[str, Any]:
        response = await self._http.get(
            f"{self.base_url}/api/queue/status",
            params={"userId": user_id},
        )
        response.raise_for_status()
        return response.json()


class ChatServerClient:
    """ChatServer(WebSocket)와 상호작용을 담당."""

    def __init__(self, base_url: str) -> None:
        self.base_url = base_url.rstrip("/")

    async def play_session(
        self,
        ticket_id: str,
        nickname: str,
        outbound_message: str,
        listen_seconds: int,
    ) -> None:
        url = f"{self.base_url}/gameserver?ticketId={ticket_id}"
        print(f"[chat-server] WebSocket 연결 시도 {url}")

        try:
            async with websockets.connect(
                url, ping_interval=30, ping_timeout=10
            ) as websocket:
                print(
                    "[chat-server] 연결 완료! /help 또는 /quit 명령을 사용할 수 있습니다."
                )
                print("- 메시지를 입력하면 실시간으로 서버에 전송됩니다.")
                if outbound_message:
                    await self._send_message(websocket, outbound_message)
                await self._interactive_loop(websocket, listen_seconds)
        except websockets.exceptions.InvalidStatusCode as exc:
            reason = _describe_invalid_status(exc.status_code)
            print(
                f"[chat-server] 연결 거부됨 status={exc.status_code} ({reason})",
                file=sys.stderr,
            )
        except OSError as exc:
            print(f"[chat-server] 소켓 연결 실패: {exc}", file=sys.stderr)

    async def _send_message(self, websocket: Any, message: str) -> None:
        if not message:
            return
        envelope = MessageSend(message=message).to_dict()
        serialized = json.dumps(envelope, ensure_ascii=False)
        await websocket.send(serialized)
        print(f"[chat-server] 송신: {message}")

    async def _interactive_loop(self, websocket: Any, listen_seconds: int) -> None:
        receiver = asyncio.create_task(
            self._receiver_loop(websocket), name="chat-receiver"
        )
        sender = asyncio.create_task(self._sender_loop(websocket), name="chat-sender")
        tasks = asyncio.gather(receiver, sender, return_exceptions=True)

        try:
            if listen_seconds > 0:
                try:
                    await asyncio.wait_for(tasks, timeout=listen_seconds)
                except asyncio.TimeoutError:
                    print(
                        "[chat-server] listen-seconds 제한에 도달하여 연결을 종료합니다."
                    )
                    await websocket.close()
            else:
                await tasks
        finally:
            for coro in (receiver, sender):
                if not coro.done():
                    coro.cancel()
            await asyncio.gather(receiver, sender, return_exceptions=True)

    async def _sender_loop(self, websocket: Any) -> None:
        print("입력 대기 중입니다. `/quit` 또는 `:q` 입력 시 세션이 종료됩니다.")
        while True:
            try:
                text = await asyncio.to_thread(input, "> ")
            except EOFError:
                text = "/quit"

            text = text.strip()
            if not text:
                continue
            if text in {"/quit", ":q", "/exit"}:
                print("[chat-server] 종료 명령을 받았습니다. 연결을 닫습니다.")
                await websocket.close()
                return
            if text == "/help":
                print("사용 가능한 명령: /quit | /help | /stats")
                continue
            if text == "/stats":
                await self._send_status_request(websocket)
                continue

            await self._send_message(websocket, text)

    async def _receiver_loop(self, websocket: Any) -> None:
        while True:
            try:
                message = await websocket.recv()
            except websockets.ConnectionClosedOK:
                print("[chat-server] 서버가 정상적으로 연결을 종료했습니다.")
                return
            except websockets.ConnectionClosedError as exc:
                print(
                    f"[chat-server] 연결이 예기치 않게 종료됨: code={exc.code}",
                    file=sys.stderr,
                )
                return

            formatted = self._format_inbound(message)
            print(f"\n[chat-server] 수신: {formatted}")

    @staticmethod
    def _format_inbound(raw: str) -> str:
        try:
            parsed = json.loads(raw)
        except json.JSONDecodeError:
            return raw

        if not isinstance(parsed, dict):
            return parsed

        msg_type = str(parsed.get("type", "")).upper()
        payload = parsed.get("payload") or {}

        if msg_type == WebSocketMessageTypes.MESSAGE_RECEIVE and isinstance(
            payload, dict
        ):
            nickname = payload.get("nickname", "unknown")
            message = payload.get("message")
            timestamp = payload.get("timestamp")
            if message:
                return f"{nickname}@{timestamp}: {message}"
        elif msg_type == WebSocketMessageTypes.SERVER_STATUS_RESPONSE and isinstance(
            payload, dict
        ):
            count = payload.get("clientCount")
            return f"[서버 인원] 현재 접속자 {count}명"
        elif msg_type == WebSocketMessageTypes.SYSTEM_MESSAGE_RECEIVE and isinstance(
            payload, dict
        ):
            message = payload.get("message")
            timestamp = payload.get("timestamp")
            if message:
                return f"[system@{timestamp}] {message}"

        return json.dumps(parsed, ensure_ascii=False)

    async def _send_status_request(self, websocket: Any) -> None:
        envelope = ServerStatusRequest().to_dict()
        serialized = json.dumps(envelope, ensure_ascii=False)
        await websocket.send(serialized)
        print("[chat-server] 서버 인원 정보를 요청했습니다.")


def _describe_invalid_status(status_code: int) -> str:
    msgs = {
        401: "잘못된 ticketId",
        409: "이미 다른 세션이 연결되어 있습니다",
    }
    return msgs.get(status_code, "서버가 연결을 거부했습니다")


def _parse_meta(pairs: list[str]) -> Dict[str, str]:
    meta: Dict[str, str] = {}
    for pair in pairs:
        if "=" not in pair:
            continue
        key, value = pair.split("=", 1)
        if key:
            meta[key] = value
    return meta


if __name__ == "__main__":
    run_cli()
