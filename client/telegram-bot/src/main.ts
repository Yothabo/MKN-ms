import { Bot } from "grammy";
import { loadConfig } from "./config.js";
import { ApiClient } from "./api-client.js";
import { registerPing } from "./commands/ping.js";
import { registerRolesCreate } from "./commands/roles_create.js";
import { registerRolesList } from "./commands/roles_list.js";
import { registerDutiesCreate } from "./commands/duties_create.js";
import { registerBranchesCreate } from "./commands/branches_create.js";
import { registerMembersCreate } from "./commands/members_create.js";
import { registerOccurrencesList } from "./commands/occurrences_list.js";

async function main(): Promise<void> {
  const config = loadConfig();
  const api = new ApiClient(config.apiBaseUrl);
  const bot = new Bot(config.botToken);

  const isAllowed = (userId: number): boolean =>
    config.adminMap.has(userId);
  const resolveAdminId = (userId: number): number | undefined =>
    config.adminMap.get(userId);

  registerPing(bot, api, isAllowed);
  registerRolesCreate(bot, api, resolveAdminId);
  registerRolesList(bot, api, isAllowed);
  registerDutiesCreate(bot, api, resolveAdminId);
  registerBranchesCreate(bot, api, resolveAdminId);
  registerMembersCreate(bot, api, resolveAdminId);
  registerOccurrencesList(bot, api, isAllowed);

  bot.catch(async (botErr) => {
    // Full detail goes to the terminal. The chat gets a short message.
    // A stack trace is not useful to an operator and, at 4000+
    // characters, exceeds Telegram's reply limit and causes a second
    // failure that masks the first.
    if (botErr.error instanceof Error) {
      console.error("Bot error:", botErr.error.stack ?? botErr.error.message);
    } else {
      console.error("Bot error:", String(botErr.error));
    }
    try {
      await botErr.ctx.reply(
        "Something went wrong. Check the bot's terminal output for the trace.",
      );
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
