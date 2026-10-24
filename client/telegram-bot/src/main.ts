import { Bot } from "grammy";
import { loadConfig } from "./config.js";
import { ApiClient } from "./api-client.js";
import { registerPing } from "./commands/ping.js";
import { registerRolesCreate } from "./commands/roles_create.js";
import { registerRolesList } from "./commands/roles_list.js";
import { registerDutiesCreate } from "./commands/duties_create.js";
import { registerBranchesCreate } from "./commands/branches_create.js";
import { registerMembersCreate } from "./commands/members_create.js";

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
