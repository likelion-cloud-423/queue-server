import chalk from "chalk";
import { QueueApiClient, QueueStatus, QueueWaitDisplay, renderStatus, sleep } from "./queue.js";
import { ChatClient } from "./chat.js";
import type { ClientConfig } from "./config.js";

export async function runClient(config: ClientConfig): Promise<void> {
  printBanner(config);

  const queueClient = new QueueApiClient(config.queueBaseUrl);
  const ticketId = await acquireTicket(queueClient, config);

  if (!ticketId) {
    console.error("티켓을 받지 못했습니다. Queue-Manager가 동작 중인지 확인하세요.");
    return;
  }

  if (config.skipChat) {
    console.log("--skip-chat 옵션이 설정되어 ChatServer 연결을 생략합니다.");
    return;
  }

  const chatClient = new ChatClient(config.chatBaseUrl);
  await chatClient.playSession({
    ticketId,
    nickname: config.nickname,
    initialMessage: config.initialMessage,
    listenSeconds: config.listenSeconds,
  });
}

async function acquireTicket(queueClient: QueueApiClient, config: ClientConfig): Promise<string | null> {
  console.log(`[queue-api] /api/queue/entry 호출 중... nickname=${config.nickname}`);
  let entry: Record<string, unknown>;

  try {
    entry = await queueClient.enterQueue(config.nickname);
  } catch (error) {
    console.error("[queue-api] 입장 요청 실패", error instanceof Error ? error.message : "");
    return null;
  }

  const status = QueueStatus.from(entry);
  renderStatus("entry 응답", status, entry);

  const fallbackUserId =
    typeof entry["userId"] === "string"
      ? entry["userId"]
      : typeof entry["user_id"] === "string"
      ? entry["user_id"]
      : undefined;
  const userId = status.userId ?? fallbackUserId;
  if (!userId) {
    console.error("[queue-api] 응답에서 userId를 찾을 수 없습니다.");
    return null;
  }

  if (status.isPromoted) {
    return status.ticketId;
  }

  return waitForTicket(queueClient, userId, config);
}

async function waitForTicket(
  queueClient: QueueApiClient,
  userId: string,
  config: ClientConfig,
): Promise<string | null> {
  const display = new QueueWaitDisplay();

  for (let attempt = 1; attempt <= config.maxPolls; attempt += 1) {
    try {
      const response = await queueClient.pollStatus(userId);
      const status = QueueStatus.from(response);
      display.emit(status, attempt, config.maxPolls);

      if (status.isPromoted) {
        return status.ticketId;
      }
    } catch (error) {
      console.error(
        `[queue-api] 상태 조회 실패 (${error instanceof Error ? error.message : "알 수 없음"}); ${config.pollInterval}s 대기 후 재시도`,
      );
    }

    await sleep(config.pollInterval * 1000);
  }

  return null;
}

function printBanner(config: ClientConfig): void {
  console.log(chalk.green("=".repeat(60)));
  console.log(chalk.bold("Queue Chat Client"));
  console.log(` - queue-api : ${config.queueBaseUrl}`);
  console.log(` - ChatServer: ${config.chatBaseUrl}`);
  console.log(` - Nickname  : ${config.nickname}`);
  console.log(chalk.green("=".repeat(60)));
}
