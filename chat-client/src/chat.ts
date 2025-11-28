import WebSocket from "ws";
import {
  WebSocketMessageTypes,
  buildMessageSend,
  buildServerStatusRequest,
  formatInboundMessage,
} from "./messages.js";
import cliTruncate from "cli-truncate";
import { createChatUi } from "./ui.js";

const SERVER_STATUS_POLL_INTERVAL_MS = 1000;
type PendingOutgoing = { id: string; text: string };

export interface ChatSessionConfig {
  ticketId: string;
  nickname: string;
  initialMessage: string;
  listenSeconds: number;
}

export class ChatClient {
  constructor(private readonly baseUrl: string) {}

  async playSession(config: ChatSessionConfig): Promise<void> {
    const url = `${this.baseUrl}/gameserver?ticketId=${encodeURIComponent(config.ticketId)}`;
    const ui = createChatUi({ nickname: config.nickname, serverUrl: url });
    ui.showSystem(`WebSocket 연결 시도 ${url}`);
    ui.showSystem(`닉네임 ${config.nickname}으로 접속합니다.`);
    ui.showStatus("연결 준비 중...");

    const ws = new WebSocket(url, { handshakeTimeout: 10000 });

    let settled = false;
    let listenTimer: NodeJS.Timeout | undefined;
    let uiStopped = false;
    let statusPoller: NodeJS.Timeout | undefined;
    let logNextServerStatusResponse = false;
    let pendingSequence = 0;
    const pendingMessages: PendingOutgoing[] = [];

    const closeConnection = (): void => {
      if (ws.readyState < WebSocket.CLOSING) {
        ws.close();
      }
    };

    const stopUi = (): void => {
      if (uiStopped) {
        return;
      }
      uiStopped = true;
      stopServerStatusPolling();
      ui.stop();
    };

    const stopServerStatusPolling = (): void => {
      if (statusPoller) {
        clearInterval(statusPoller);
        statusPoller = undefined;
      }
    };

    const pollServerStatus = (): void => {
      if (ws.readyState !== WebSocket.OPEN) {
        return;
      }
      try {
        ws.send(JSON.stringify(buildServerStatusRequest()));
      } catch {
        // Ignore send failures (connection may be closing)
      }
    };

    const allocatePendingId = (): string => `pending-${Date.now()}-${++pendingSequence}`;

    const enqueuePendingMessage = (text: string): string => {
      const id = allocatePendingId();
      pendingMessages.push({ id, text });
      ui.showPendingOutgoing(id, text);
      return id;
    };

    const removePendingById = (id: string): PendingOutgoing | null => {
      const index = pendingMessages.findIndex((entry) => entry.id === id);
      if (index === -1) {
        return null;
      }
      const [pending] = pendingMessages.splice(index, 1);
      return pending;
    };

    const resolvePendingMessage = (text?: string, label?: string): void => {
      if (pendingMessages.length === 0) {
        if (text) {
          ui.showIncoming(text, label);
        }
        return;
      }
      let index = typeof text === "string" ? pendingMessages.findIndex((entry) => entry.text === text) : -1;
      if (index < 0) {
        index = 0;
      }
      const [pending] = pendingMessages.splice(index, 1);
      ui.confirmPendingOutgoing(pending.id, text ?? pending.text, label);
    };

    const handlePendingSendFailure = (id: string, reason: string): void => {
      const pending = removePendingById(id);
      if (pending) {
        ui.confirmPendingOutgoing(pending.id, `${pending.text} (전송 실패)`);
      }
      ui.showStatus(`메시지 전송 실패: ${reason}`);
    };

    const sendChatMessage = (text: string): void => {
      const pendingId = enqueuePendingMessage(text);
      try {
        ws.send(JSON.stringify(buildMessageSend(text)));
      } catch (error) {
        handlePendingSendFailure(pendingId, error instanceof Error ? error.message : String(error));
      }
    };

    const startServerStatusPolling = (): void => {
      if (statusPoller) {
        return;
      }
      pollServerStatus();
      statusPoller = setInterval(pollServerStatus, SERVER_STATUS_POLL_INTERVAL_MS);
    };

    ui.onQuit(() => {
      ui.showSystem("사용자 종료 요청을 감지했습니다. 연결을 정리합니다.");
      closeConnection();
    });

    const handleUserInput = (raw: string): void => {
      const text = raw.trim();
      if (text.length === 0) {
        return;
      }

      if (isQuitCommand(text)) {
        ui.showSystem("종료 명령을 받았습니다. 연결을 닫습니다.");
        closeConnection();
        return;
      }

      if (text === "/help") {
        ui.showSystem("명령: /help | /quit | /stats · Ctrl+C 또는 Esc로도 종료할 수 있습니다.");
        return;
      }

      if (text === "/stats") {
        if (ws.readyState !== WebSocket.OPEN) {
          ui.showSystem("아직 연결이 준비되지 않았습니다.");
          return;
        }
        logNextServerStatusResponse = true;
        ws.send(JSON.stringify(buildServerStatusRequest()));
        ui.showSystem("서버 상태를 요청했습니다.");
        return;
      }

      if (ws.readyState !== WebSocket.OPEN) {
        ui.showSystem("아직 연결이 준비되지 않았습니다.");
        return;
      }

      sendChatMessage(text);
    };

    ui.onInput(handleUserInput);
    ui.start();

    if (config.listenSeconds > 0) {
      listenTimer = setTimeout(() => {
        ui.showSystem("listen-seconds 제한에 도달하여 연결을 종료합니다.");
        closeConnection();
      }, config.listenSeconds * 1000);
    }

    ws.on("open", () => {
      ui.showStatus("연결 완료! 상대방을 기다리는 중입니다.");
      ui.showSystem("메시지를 입력해 대화를 시작하세요. /help로 명령을 확인할 수 있습니다.");
      if (config.initialMessage) {
        sendChatMessage(config.initialMessage);
      }
      startServerStatusPolling();
    });

    ws.on("message", (data) => {
      const payload = typeof data === "string" ? safeJsonParse(data) : safeJsonParse(data.toString());
      const envelope = getWebSocketEnvelope(payload);

      if (envelope?.type === WebSocketMessageTypes.SERVER_STATUS_RESPONSE) {
        const count = extractServerStatusCount(envelope.payload);
        ui.updateCornerStatus(
          count !== null ? `[서버 인원] 현재 접속자 ${count}명` : "[서버 인원] 정보 없음",
        );
        if (logNextServerStatusResponse) {
          const formatted = formatInboundMessage(payload);
          if (formatted) {
            ui.showIncoming(formatted);
          }
        }
        logNextServerStatusResponse = false;
        return;
      }

      if (envelope?.type === WebSocketMessageTypes.SYSTEM_MESSAGE_RECEIVE) {
        const systemMeta = simplifySystemMessage(envelope.payload);
        if (systemMeta) {
          ui.showIncoming(systemMeta.text, systemMeta.label);
        } else {
          const formatted = formatInboundMessage(payload);
          if (formatted) {
            ui.showSystem(formatted);
          }
        }
        return;
      }

      const incomingMeta = decodeIncomingMessage(envelope?.payload);
      if (incomingMeta) {
        const label = `${incomingMeta.nickname} ${incomingMeta.time}`;
        if (incomingMeta.nickname === config.nickname) {
          resolvePendingMessage(incomingMeta.text, label);
          return;
        }
        ui.showIncoming(incomingMeta.text, label);
        return;
      }

      const formatted = formatInboundMessage(payload);
      if (formatted) {
        ui.showIncoming(formatted);
      }
    });

    ws.on("close", (code) => {
      if (settled) {
        return;
      }
      ui.updateCornerStatus("[서버 인원] 연결 종료됨");
      ui.showStatus(`연결 종료 (code=${code}).`);
    });

    ws.on("error", (error) => {
      if (settled) {
        return;
      }
      ui.showSystem(`소켓 오류: ${error.message}`);
    });

    ws.on("unexpected-response", (_req, res) => {
      if (settled) {
        return;
      }
      ui.showSystem(`연결 거부됨 status=${res.statusCode}`);
    });

    const connectionPromise = new Promise<void>((resolve) => {
      const waiter = (): void => {
        if (!settled) {
          settled = true;
          if (listenTimer) {
            clearTimeout(listenTimer);
            listenTimer = undefined;
          }
          stopUi();
        }
        resolve();
      };
      ws.once("close", waiter);
      ws.once("error", waiter);
      ws.once("unexpected-response", waiter);
    });

    await connectionPromise;
  }
}

function safeJsonParse(input: string): unknown {
  try {
    return JSON.parse(input);
  } catch {
    return input;
  }
}

function isQuitCommand(input: string): boolean {
  const lowered = input.toLowerCase();
  return lowered === "/quit" || lowered === ":q" || lowered === "/exit";
}

type WebSocketEnvelope = {
  type?: string;
  payload?: unknown;
};

function getWebSocketEnvelope(value: unknown): WebSocketEnvelope | null {
  const record = asRecord(value);
  if (!record) {
    return null;
  }
  const rawType = record.type;
  const type = typeof rawType === "string" ? rawType : rawType == null ? undefined : String(rawType);
  return { type, payload: record.payload };
}

function extractServerStatusCount(payload: unknown): number | null {
  const record = asRecord(payload);
  if (!record) {
    return null;
  }
  const candidate = record.clientCount ?? record.ClientCount ?? record.client_count;
  return normalizeNumeric(candidate);
}

function asRecord(value: unknown): Record<string, unknown> | null {
  if (typeof value !== "object" || value === null) {
    return null;
  }
  return value as Record<string, unknown>;
}

function normalizeNumeric(value: unknown): number | null {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string" && value.trim().length > 0) {
    const parsed = Number(value.trim());
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

interface IncomingMeta {
  nickname: string;
  text: string;
  time: string;
}

function decodeIncomingMessage(payload: unknown): IncomingMeta | null {
  const record = asRecord(payload);
  if (!record) {
    return null;
  }
  const nickname = record.nickname;
  const message = record.message;
  if (typeof nickname !== "string" || typeof message !== "string") {
    return null;
  }
  const timestamp = record.timestamp ?? record.time ?? null;
  return {
    nickname,
    text: message,
    time: formatShortTime(timestamp),
  };
}

function formatShortTime(value: unknown): string {
  const date = toDate(value);
  const pad2 = (num: number): string => String(num).padStart(2, "0");
  return `${pad2(date.getHours())}:${pad2(date.getMinutes())}:${pad2(date.getSeconds())}`;
}

function toDate(value: unknown): Date {
  if (value instanceof Date && Number.isFinite(value.getTime())) {
    return value;
  }
  if (typeof value === "number" && Number.isFinite(value)) {
    return new Date(value);
  }
  if (typeof value === "string" && value.trim().length > 0) {
    const parsed = new Date(value.trim());
    if (Number.isFinite(parsed.getTime())) {
      return parsed;
    }
  }
  return new Date();
}

function simplifySystemMessage(payload: unknown): { label: string; text: string } | null {
  const record = asRecord(payload);
  if (!record) {
    return null;
  }
  const raw = record.message;
  if (typeof raw !== "string") {
    return null;
  }
  const lines = raw
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
  if (lines.length === 0) {
    return null;
  }
  const header = lines[0];
  const body = lines.slice(1).join(" ").trim() || header;
  const timestamp = record.timestamp ?? record.time ?? extractSystemTimestamp(header) ?? null;
  const label = `시스템 ${formatShortTime(timestamp)}`;
  return { label, text: body };
}

function extractSystemTimestamp(header: string): string | null {
  const match = header.match(/system@([^]\]\s]+)/);
  if (!match) {
    return null;
  }
  return match[1];
}
