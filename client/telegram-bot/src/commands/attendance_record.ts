import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerAttendanceRecord(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("attendance_record", async (ctx) => {
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
    const parts = arg.split(/\s+/).filter((p) => p !== "");
    if (parts.length !== 2) {
      await ctx.reply(
        "Usage: /attendance_record <memberId> <occurrenceId>\n" +
        "Example: /attendance_record 1 1",
      );
      return;
    }

    const memberId = Number(parts[0]);
    const occurrenceId = Number(parts[1]);
    if (!Number.isInteger(memberId) || memberId <= 0) {
      await ctx.reply("memberId must be a positive integer.");
      return;
    }
    if (!Number.isInteger(occurrenceId) || occurrenceId <= 0) {
      await ctx.reply("occurrenceId must be a positive integer.");
      return;
    }

    const result = await api.recordAttendance(memberId, occurrenceId);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    const r = result.data;
    if (r.recorded) {
      await ctx.reply(
        `Attendance recorded: member ${memberId}, occurrence ${occurrenceId} ` +
        `(record id ${r.recordId}).`,
      );
    } else {
      await ctx.reply(
        `Attendance already on file for member ${memberId}, occurrence ${occurrenceId} ` +
        `(record id ${r.recordId}). No new row created.`,
      );
    }
  });
}
