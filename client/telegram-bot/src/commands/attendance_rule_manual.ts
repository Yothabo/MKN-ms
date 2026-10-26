import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerAttendanceRuleManual(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("attendance_rule_manual", async (ctx) => {
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
    const parts = arg.split(/\s+/).filter((p) => p !== "");
    if (parts.length !== 1) {
      await ctx.reply(
        "Usage: /attendance_rule_manual <ruleId>\n" +
        "The rule must have TriggerType = Manual.",
      );
      return;
    }

    const ruleId = Number(parts[0]);
    if (!Number.isInteger(ruleId) || ruleId <= 0) {
      await ctx.reply("ruleId must be a positive integer.");
      return;
    }

    const result = await api.attendanceRuleManual(ruleId, actingAdminId);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    const r = result.data;
    const lines = [
      `Attendance rule engine: ${r.status}.`,
      `Rules evaluated: ${r.rulesEvaluated}.`,
      `Members evaluated: ${r.membersEvaluated}.`,
      `Status changes applied: ${r.statusChangesApplied}.`,
      `Notifications dispatched: ${r.notificationsDispatched}.`,
    ];
    if (r.errorCode) {
      lines.push(`Error code: ${r.errorCode}`);
    }
    if (r.errorDetail) {
      lines.push(`Detail: ${r.errorDetail.slice(0, 300)}`);
    }
    await ctx.reply(lines.join("\n"));
  });
}
