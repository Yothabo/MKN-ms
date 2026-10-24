import type { Bot } from "grammy";
import { ApiClient, renderFailure, singleLine } from "../api-client.js";

export function registerRolesCreate(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("roles_create", async (ctx) => {
    const userId = ctx.from?.id;
    if (userId === undefined || resolveAdminId(userId) === undefined) {
      await ctx.reply("You are not on the operator allowlist.");
      return;
    }

    const args = ctx.match.trim();
    if (args === "") {
      await ctx.reply("Usage: /roles_create <name>");
      return;
    }
    const name = singleLine(args);
    if (name === null) {
      await ctx.reply("Name must be a single line. One command per message.");
      return;
    }

    const actingAdminId = resolveAdminId(userId)!;
    const result = await api.createRole(name, actingAdminId);

    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    await ctx.reply(
      `Role created: ${result.data.name} (id ${result.data.roleId}).`,
    );
  });
}
