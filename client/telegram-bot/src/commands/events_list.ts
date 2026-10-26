import type { Bot } from "grammy";
import { ApiClient, singleLine } from "../api-client.js";

const MAX_ROWS = 20;

export function registerEventsList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("events_list", async (ctx) => {
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
      const events = await api.listEvents();
      if (events.length === 0) {
        await ctx.reply("No events.");
        return;
      }
      const rows = events.slice(0, MAX_ROWS).map(
        (e) => `\u2022 id ${e.eventId} \u2014 ${e.name} ` +
               `(${e.startDate} to ${e.endDate}, ` +
               `register ${e.usesAttendanceRegister === true ? "on" : "off"})`,
      );
      const suffix = events.length > MAX_ROWS
        ? `\n\u2026 and ${events.length - MAX_ROWS} more.` : "";
      await ctx.reply(`Events:\n${rows.join("\n")}${suffix}`);
    } catch (err) {
      const m = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Request failed: ${m.slice(0, 300)}`);
    }
  });
}
