import type { Bot } from "grammy";
import { ApiClient, renderFailure, singleLine, commandArgument } from "../api-client.js";

export function registerBranchesCreate(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("branches_create", async (ctx) => {
    const userId = ctx.from?.id;
    const actingAdminId = userId !== undefined ? resolveAdminId(userId) : undefined;
    if (actingAdminId === undefined) {
      await ctx.reply("You are not on the operator allowlist.");
      return;
    }

    const text = ctx.message?.text;
    if (singleLine(text) === null) {
      await ctx.reply("One command per message. Do not paste multiple lines.");
      return;
    }

    const arg = commandArgument(text);
    if (arg === "") {
      await ctx.reply("Usage: /branches_create <name> | <location>");
      return;
    }

    const sep = arg.indexOf("|");
    if (sep < 0) {
      await ctx.reply("Usage: /branches_create <name> | <location>");
      return;
    }

    const name = arg.slice(0, sep).trim();
    const location = arg.slice(sep + 1).trim();
    if (name === "" || location === "") {
      await ctx.reply("Usage: /branches_create <name> | <location>");
      return;
    }

    const result = await api.createBranch(name, location, actingAdminId);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(`Branch created: ${result.data.name} (id ${result.data.branchId}).`);
  });
}
