import { Bot } from "grammy";
import { loadConfig } from "./config.js";
import { ApiClient } from "./api-client.js";
import { registerPing } from "./commands/ping.js";

async function main(): Promise<void> {
  const config = loadConfig();
  const api = new ApiClient(config.apiBaseUrl);
  const bot = new Bot(config.botToken);

  const isAllowed = (userId: number): boolean =>
    config.adminMap.has(userId);

  registerPing(bot, api, isAllowed);

  bot.catch(async (botErr) => {
    const message =
      botErr.error instanceof Error
        ? botErr.error.message
        : String(botErr.error);
    console.error("Bot error:", message);
    try {
      await botErr.ctx.reply(`Bot error: ${message}`);
    } catch {
      // Reply failed too; nothing further to do.
    }
  });

  console.log(`MKN-MS bot starting. API at ${config.apiBaseUrl}.`);
  await bot.start();
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
