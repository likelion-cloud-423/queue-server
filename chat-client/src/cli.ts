import { Command } from "commander";
import { buildConfig, CliOptions } from "./config.js";
import { runClient } from "./app.js";

const program = new Command();

program
  .description("queue-api ↔ ChatServer 연동용 Node.js 클라이언트")
  .option("--queue-base-url <url>", "queue-api 베이스 URL")
  .option("--chat-base-url <url>", "ChatServer 베이스 URL")
  .option("--nickname <name>", "대기열/채팅에 사용할 닉네임")
  .option("--poll-interval <seconds>", "상태 폴링 간격", parseFloat)
  .option("--max-polls <count>", "최대 폴링 횟수", (value) => parseInt(value, 10))
  .option("--skip-chat", "채팅 연결을 생략")
  .option("--message <text>", "연결 직후 자동으로 전송할 메시지")
  .option("--listen-seconds <seconds>", "세션을 강제로 종료할 시간", (value) => parseInt(value, 10));

async function main(): Promise<void> {
    console.log(process.stdout.isTTY)
  const options = program.parse(process.argv).opts() as CliOptions;
  const config = buildConfig(options);

  try {
    await runClient(config);
  } catch (error) {
    console.error("실행 중 오류가 발생했습니다:", error instanceof Error ? error.message : error);
    process.exit(1);
  }
}

if (import.meta.main) {
  main();
}
