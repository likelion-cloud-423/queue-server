from __future__ import annotations

from dataclasses import asdict, dataclass, is_dataclass
from typing import Any, Dict, Generic, Literal, TypeVar


PayloadT = TypeVar("PayloadT")


class WebSocketEnvelope(Generic[PayloadT]):
    """기본 웹소켓 메시지 포맷 (type + payload)을 표현."""

    #: 자식 클래스에서 literals로 덮어쓰는 것을 기대한다.
    type: str
    payload: PayloadT

    def __init__(self, *, type: str, payload: PayloadT) -> None:
        self.type = type
        self.payload = payload

    def to_dict(self) -> Dict[str, Any]:
        payload = self.payload
        if is_dataclass(payload):
            payload = asdict(payload)
        return {"type": self.type, "payload": payload}


class WebSocketMessageTypes:
    MESSAGE_SEND: Literal["MESSAGE_SEND"] = "MESSAGE_SEND"
    MESSAGE_RECEIVE: Literal["MESSAGE_RECEIVE"] = "MESSAGE_RECEIVE"
    SERVER_STATUS_REQUEST: Literal["SERVERSTATUS_REQUEST"] = "SERVERSTATUS_REQUEST"
    SERVER_STATUS_RESPONSE: Literal["SERVERSTATUS_RESPONSE"] = "SERVERSTATUS_RESPONSE"
    SYSTEM_MESSAGE_RECEIVE: Literal["SYSTEM_MESSAGE_RECEIVE"] = "SYSTEM_MESSAGE_RECEIVE"


@dataclass(slots=True)
class MessageSendPayload:
    """사용자가 서버에 보내는 채팅 메시지."""

    message: str


@dataclass(slots=True)
class MessageReceivePayload:
    """서버가 브로드캐스트하는 채팅 메시지."""

    message: str
    nickname: str
    timestamp: str


@dataclass(slots=True)
class ServerStatusRequestPayload:
    """서버 인원 수 조회 요청 (내용 없음)."""

    pass


@dataclass(slots=True)
class ServerStatusResponsePayload:
    """현재 서버 접속자 수 응답."""

    clientCount: int


@dataclass(slots=True)
class SystemMessageReceivePayload:
    """시스템에서 내려오는 공지 메시지."""

    message: str
    timestamp: str


class MessageSend(WebSocketEnvelope[MessageSendPayload]):
    type: Literal["MESSAGE_SEND"]

    def __init__(self, message: str) -> None:
        super().__init__(
            type=WebSocketMessageTypes.MESSAGE_SEND,
            payload=MessageSendPayload(message=message),
        )


class MessageReceive(WebSocketEnvelope[MessageReceivePayload]):
    type: Literal["MESSAGE_RECEIVE"]

    def __init__(self, message: str, nickname: str, timestamp: str) -> None:
        super().__init__(
            type=WebSocketMessageTypes.MESSAGE_RECEIVE,
            payload=MessageReceivePayload(message=message, nickname=nickname, timestamp=timestamp),
        )


class ServerStatusRequest(WebSocketEnvelope[ServerStatusRequestPayload]):
    type: Literal["SERVERSTATUS_REQUEST"]

    def __init__(self) -> None:
        super().__init__(
            type=WebSocketMessageTypes.SERVER_STATUS_REQUEST,
            payload=ServerStatusRequestPayload(),
        )


class ServerStatusResponse(WebSocketEnvelope[ServerStatusResponsePayload]):
    type: Literal["SERVERSTATUS_RESPONSE"]

    def __init__(self, client_count: int) -> None:
        super().__init__(
            type=WebSocketMessageTypes.SERVER_STATUS_RESPONSE,
            payload=ServerStatusResponsePayload(clientCount=client_count),
        )


class SystemMessageReceive(WebSocketEnvelope[SystemMessageReceivePayload]):
    type: Literal["SYSTEM_MESSAGE_RECEIVE"]

    def __init__(self, message: str, timestamp: str) -> None:
        super().__init__(
            type=WebSocketMessageTypes.SYSTEM_MESSAGE_RECEIVE,
            payload=SystemMessageReceivePayload(message=message, timestamp=timestamp),
        )
