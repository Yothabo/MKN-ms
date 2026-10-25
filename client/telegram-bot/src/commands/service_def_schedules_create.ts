import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerServiceDefSchedulesCreate(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("service_def_schedules_create", async (ctx) => {
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
    if (parts.length !== 3) {
      await ctx.reply(
        "Usage: /service_def_schedules_create <serviceDefId> <timeSlotId> <startTime>\n" +
        "Example: /service_def_schedules_create 1 1 09:00",
      );
      return;
    }

    const serviceDefId = Number(parts[0]);
    const timeSlotId = Number(parts[1]);
    const startTime = parts[2];

    if (!Number.isInteger(serviceDefId) || serviceDefId <= 0) {
      await ctx.reply("serviceDefId must be a positive integer.");
      return;
    }
    if (!Number.isInteger(timeSlotId) || timeSlotId <= 0) {
      await ctx.reply("timeSlotId must be a positive integer.");
      return;
    }

    const result = await api.createServiceSchedule(
      serviceDefId, timeSlotId, startTime, actingAdminId);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(
      `Schedule created: id ${result.data.scheduleId} ` +
      `(def ${result.data.serviceDefId}, slot ${result.data.timeSlotId}, ` +
      `start ${result.data.startTime}).`,
    );
  });
}
