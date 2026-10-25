import type { Bot } from "grammy";
import { ApiClient, singleLine, commandArgument } from "../api-client.js";

const MAX_ROWS = 20;

export function registerOccurrencesList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("occurrences_list", async (ctx) => {
    const userId = ctx.from?.id;
    if (userId === undefined || !isAllowed(userId)) {
      await ctx.reply("You are not on the operator allowlist.");
      return;
    }

    const text = ctx.message?.text;
    if (singleLine(text) === null) {
      await ctx.reply("One command per message. Do not paste multiple lines.");
      return;
    }

    // Optional positional args: <fromDate> [toDate]. Both ISO dates.
    const arg = commandArgument(text);
    let fromDate: string | undefined;
    let toDate: string | undefined;
    if (arg !== "") {
      const parts = arg.split(/\s+/);
      if (parts.length < 1 || parts.length > 2) {
        await ctx.reply("Usage: /occurrences_list [fromDate] [toDate]");
        return;
      }
      fromDate = parts[0];
      if (parts.length === 2) {
        toDate = parts[1];
      }
    }

    try {
      const occurrences = await api.listOccurrences(fromDate, toDate);
      if (occurrences.length === 0) {
        await ctx.reply("No occurrences.");
        return;
      }

      const rows = occurrences.slice(0, MAX_ROWS).map((o) =>
        `• id ${o.occurrenceId} — ${o.date} ` +
        `(schedule ${o.scheduleId ?? "-"}, fill ${o.fillStatusId ?? "-"})`,
      );
      const suffix =
        occurrences.length > MAX_ROWS
          ? `\n… and ${occurrences.length - MAX_ROWS} more.`
          : "";
      await ctx.reply(`Occurrences:\n${rows.join("\n")}${suffix}`);
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Request failed: ${message.slice(0, 300)}`);
    }
  });
}
