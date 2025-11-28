import { randomBytes } from "node:crypto";

export interface ClientConfig {
  queueBaseUrl: string;
  chatBaseUrl: string;
  nickname: string;
  pollInterval: number;
  maxPolls: number;
  skipChat: boolean;
  initialMessage: string;
  listenSeconds: number;
}

export interface CliOptions {
  queueBaseUrl?: string;
  chatBaseUrl?: string;
  nickname?: string;
  pollInterval?: number;
  maxPolls?: number;
  skipChat?: boolean;
  message?: string;
  listenSeconds?: number;
}

const DEFAULT_QUEUE_BASE = "http://localhost:8080";
const DEFAULT_CHAT_BASE = "ws://localhost:8081";

export function buildConfig(options: CliOptions): ClientConfig {
  const nickname = options.nickname?.trim();
  const initialMessage = (options.message ?? "").trim();

  return {
    queueBaseUrl: normalizeUrl(
      options.queueBaseUrl ?? process.env.QUEUE_API_BASE_URL ?? DEFAULT_QUEUE_BASE,
    ),
    chatBaseUrl: normalizeUrl(
      options.chatBaseUrl ?? process.env.CHAT_SERVER_BASE_URL ?? DEFAULT_CHAT_BASE,
    ),
    nickname: nickname && nickname.length > 0 ? nickname : generateNickname(),
    pollInterval: normalizeNumber(options.pollInterval, 2),
    maxPolls: normalizePositiveInteger(options.maxPolls, 60),
    skipChat: options.skipChat ?? false,
    initialMessage,
    listenSeconds: normalizePositiveInteger(options.listenSeconds, 0),
  };
}

function normalizeUrl(input: string): string {
  return input.replace(/\/+$/u, "");
}

function generateNickname(): string {
  const suffix = cryptoRandomSuffix(6);
  return `ranger-${suffix}`;
}

function cryptoRandomSuffix(length: number): string {
  const alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
  const bytes = randomBytes(length);
  return Array.from(bytes)
    .map((value) => alphabet[value % alphabet.length])
    .join("");
}

function normalizeNumber(value: number | undefined, fallback: number): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function normalizePositiveInteger(value: number | undefined, fallback: number): number {
  const candidate = normalizeNumber(value, fallback);
  if (candidate <= 0) {
    return fallback;
  }
  return Math.floor(candidate);
}
