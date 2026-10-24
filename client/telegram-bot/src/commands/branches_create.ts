import type { Bot } from "grammy";
import { ApiClient, renderFailure, singleLine } from "../api-client.js";

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

    const match = ctx.match.trim();
    if (match === "") {
      await ctx.reply("Usage: /branches_create <name> | <location>");
      return;
    }

    const sep = match.indexOf("|");
    if (sep < 0) {
      await ctx.reply("Usage: /branches_create <name> | <location>");
      return;
    }

    const nameRaw = match.slice(0, sep).trim();
    const locationRaw = match.slice(sep + 1).trim();
    if (nameRaw === "" || locationRaw === "") {
      await ctx.reply("Usage: /branches_create <name> | <location>");
      return;
    }
    const name = singleLine(nameRaw);
    const location = singleLine(locationRaw);
    if (name === null || location === null) {
      await ctx.reply("Name and location must each be one line. One command per message.");
      return;
    }

    const result = await api.createBranch(name, location, actingAdminId);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(
      `Branch created: ${result.data.name} (id ${result.data.branchId}).`,
    );
  });
}
