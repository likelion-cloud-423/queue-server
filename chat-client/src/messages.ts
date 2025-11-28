export const WebSocketMessageTypes = {
  MESSAGE_SEND: "MESSAGE_SEND",
  MESSAGE_RECEIVE: "MESSAGE_RECEIVE",
  SERVER_STATUS_REQUEST: "SERVERSTATUS_REQUEST",
  SERVER_STATUS_RESPONSE: "SERVERSTATUS_RESPONSE",
  SYSTEM_MESSAGE_RECEIVE: "SYSTEM_MESSAGE_RECEIVE",
} as const;

export function buildMessageSend(message: string): unknown {
  return {
    type: WebSocketMessageTypes.MESSAGE_SEND,
    payload: { message },
  };
}

export function buildServerStatusRequest(): unknown {
  return {
    type: WebSocketMessageTypes.SERVER_STATUS_REQUEST,
    payload: {},
  };
}

export function formatInboundMessage(payload: unknown): string {
  if (typeof payload !== "object" || payload === null) {
    return String(payload);
  }

  const type = String((payload as Record<string, unknown>).type ?? "").toUpperCase();
  const data = (payload as Record<string, unknown>).payload;
  if (type === WebSocketMessageTypes.MESSAGE_RECEIVE && isPlainObject(data)) {
    const { nickname, message, timestamp } = data;
    if (typeof message === "string") {
      const author = typeof nickname === "string" ? nickname : "unknown";
      const ts = typeof timestamp === "string" ? timestamp : "";
      return `${author}${ts ? `@${ts}` : ""}: ${message}`;
    }
  }

  if (type === WebSocketMessageTypes.SERVER_STATUS_RESPONSE && isPlainObject(data)) {
    const count = data.clientCount;
    if (typeof count === "number") {
      return `[서버 인원] 현재 접속자 ${count}명`;
    }
  }

  if (type === WebSocketMessageTypes.SYSTEM_MESSAGE_RECEIVE && isPlainObject(data)) {
    const { message, timestamp } = data;
    if (typeof message === "string") {
      const ts = typeof timestamp === "string" ? timestamp : "";
      return `[system${ts ? `@${ts}` : ""}] ${message}`;
    }
  }

  return JSON.stringify(payload);
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}
