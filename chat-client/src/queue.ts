import axios, { AxiosInstance } from "axios";

export class QueueApiClient {
  private readonly http: AxiosInstance;

  constructor(baseUrl: string) {
    this.http = axios.create({
      baseURL: baseUrl,
      timeout: 10000,
    });
  }

  async enterQueue(nickname: string): Promise<Record<string, unknown>> {
    const response = await this.http.post("/api/queue/entry", { nickname });
    return response.data;
  }

  async pollStatus(userId: string): Promise<Record<string, unknown>> {
    const response = await this.http.get("/api/queue/status", {
      params: { userId },
    });
    return response.data;
  }
}

export class QueueStatus {
  constructor(
    public readonly status: string,
    public readonly rank: number | null,
    public readonly ticketId: string | null,
    public readonly userId: string | null,
  ) {}

  get isPromoted(): boolean {
    const promotedSet = new Set(["PROMOTED", "READY", "GRANTED"]);
    return Boolean(this.ticketId) || promotedSet.has(this.status.toUpperCase());
  }

  static from(data: Record<string, unknown> | null | undefined): QueueStatus {
    const payload = data ?? {};
    const status = String(payload.status ?? "UNKNOWN").toUpperCase();
    const rawRank = payload.rank;
    const parsedRank =
      typeof rawRank === "number"
        ? rawRank
        : typeof rawRank === "string"
        ? Number(rawRank)
        : null;
    const rank = parsedRank !== null && Number.isFinite(parsedRank) ? parsedRank : null;
    const ticketId = (payload.ticketId as string | undefined) ?? (payload.ticket_id as string | undefined) ?? null;
    const userId = (payload.userId as string | undefined) ?? (payload.user_id as string | undefined) ?? null;
    return new QueueStatus(status, rank, ticketId, userId);
  }
}

export class QueueWaitDisplay {
  private readonly start = Date.now();
  private lastRank: number | null = null;

  emit(status: QueueStatus, attempt: number, maxAttempts: number): void {
    const elapsed = Math.floor((Date.now() - this.start) / 1000);
    const rankTxt = status.rank === null ? "미확인" : String(status.rank);
    const movement = this.computeMovement(status.rank);
    const ticketTxt = status.ticketId ? `, ticketId=${status.ticketId}` : "";

    console.log(
      `[queue] t+${elapsed.toString().padStart(2, "0")}s / 시도 ${attempt}/${maxAttempts} => status=${status.status} rank=${rankTxt}${movement}${ticketTxt}`,
    );

    this.lastRank = status.rank;
  }

  private computeMovement(currentRank: number | null): string {
    if (this.lastRank === null || currentRank === null) {
      return "";
    }
    const delta = this.lastRank - currentRank;
    if (delta > 0) {
      return ` (앞으로 ${delta}칸 진입)`;
    }
    if (delta < 0) {
      return ` (뒤로 ${Math.abs(delta)}칸 밀림)`;
    }
    return "";
  }
}

export function renderStatus(prefix: string, status: QueueStatus, raw: Record<string, unknown>): void {
  const rank = status.rank === null ? "미확인" : status.rank;
  const ticket = status.ticketId ?? "-";
  const user = status.userId ?? "-";
  console.log(`[${prefix}] status=${status.status} rank=${rank} ticketId=${ticket} userId=${user}`);
  if (Object.keys(raw).length > 0) {
    console.log(`  raw: ${JSON.stringify(raw)}`);
  }
}

export function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
