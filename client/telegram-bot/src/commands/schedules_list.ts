import type { Bot } from "grammy";
import { ApiClient, singleLine, commandArgument } from "../api-client.js";

const MAX_ROWS = 20;

export function registerSchedulesList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("schedules_list", async (ctx) => {
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

    const arg = commandArgument(text);
    let serviceDefId: number | undefined;
    if (arg !== "") {
      const n = Number(arg);
      if (!Number.isInteger(n) || n <= 0) {
        await ctx.reply("Usage: /schedules_list [serviceDefId]");
        return;
      }
      serviceDefId = n;
    }

    try {
      const schedules = await api.listServiceSchedules(serviceDefId);
      if (schedules.length === 0) {
        await ctx.reply("No schedules.");
        return;
      }
      const rows = schedules.slice(0, MAX_ROWS).map(
        (s) => `• id ${s.scheduleId} — def ${s.serviceDefId}, slot ${s.timeSlotId}, start ${s.startTime}`,
      );
      const suffix = schedules.length > MAX_ROWS
        ? `\n… and ${schedules.length - MAX_ROWS} more.` : "";
      await ctx.reply(`Schedules:\n${rows.join("\n")}${suffix}`);
    } catch (err) {
      const m = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Request failed: ${m.slice(0, 300)}`);
    }
  });
}
