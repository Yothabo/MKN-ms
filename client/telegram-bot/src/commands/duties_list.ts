import type { Bot } from "grammy";
import { ApiClient, singleLine } from "../api-client.js";

const MAX_ROWS = 20;

export function registerDutiesList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("duties_list", async (ctx) => {
    const userId = ctx.from?.id;
    if (userId === undefined || !isAllowed(userId)) {
      await ctx.reply("You are not on the operator allowlist.");
      return;
    }

    if (singleLine(ctx.message?.text) === null) {
      await ctx.reply("One command per message. Do not paste multiple lines.");
      return;
    }

    try {
      const duties = await api.listDuties();
      if (duties.length === 0) {
        await ctx.reply("No duties.");
        return;
      }
      const rows = duties.slice(0, MAX_ROWS).map(
        (d) => `• ${d.name} — id ${d.dutyId}`,
      );
      const suffix = duties.length > MAX_ROWS
        ? `\n… and ${duties.length - MAX_ROWS} more.` : "";
      await ctx.reply(`Duties:\n${rows.join("\n")}${suffix}`);
    } catch (err) {
      const m = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Request failed: ${m.slice(0, 300)}`);
    }
  });
}
