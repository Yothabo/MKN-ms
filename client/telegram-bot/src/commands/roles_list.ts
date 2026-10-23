import type { Bot } from "grammy";
import { ApiClient } from "../api-client.js";

const MAX_ROWS = 20;

export function registerRolesList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("roles_list", async (ctx) => {
    const userId = ctx.from?.id;
    if (userId === undefined || !isAllowed(userId)) {
      await ctx.reply("You are not on the operator allowlist.");
      return;
    }

    const roles = await api.listRoles();
    if (roles.length === 0) {
      await ctx.reply("No roles.");
      return;
    }

    const rows = roles
      .slice(0, MAX_ROWS)
      .map((r) => `• ${r.name} — id ${r.roleId}`);
    const suffix =
      roles.length > MAX_ROWS
        ? `\n… and ${roles.length - MAX_ROWS} more.`
        : "";
    await ctx.reply(`Roles:\n${rows.join("\n")}${suffix}`);
  });
}
