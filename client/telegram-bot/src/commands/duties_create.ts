import type { Bot } from "grammy";
import { ApiClient, renderFailure, singleLine } from "../api-client.js";

export function registerDutiesCreate(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("duties_create", async (ctx) => {
    const userId = ctx.from?.id;
    const actingAdminId = userId !== undefined ? resolveAdminId(userId) : undefined;
    if (actingAdminId === undefined) {
      await ctx.reply("You are not on the operator allowlist.");
      return;
    }

    const raw = ctx.match.trim();
    if (raw === "") {
      await ctx.reply("Usage: /duties_create <name>");
      return;
    }
    const name = singleLine(raw);
    if (name === null) {
      await ctx.reply("Name must be a single line. One command per message.");
      return;
    }

    const result = await api.createDuty(name, actingAdminId);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(`Duty created: ${result.data.name} (id ${result.data.dutyId}).`);
  });
}
