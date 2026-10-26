import type { Bot } from "grammy";
import { ApiClient, renderFailure, singleLine, commandArgument } from "../api-client.js";

const USAGE =
  "Usage: /program_items_create <programId>|<seq>|<title>|<scheduledStart>|<scheduledEnd>|<serviceDefId>\n" +
  "Example: /program_items_create 1|1|Opening Service|2026-11-01T09:00:00+02:00|2026-11-01T10:30:00+02:00|1\n" +
  "Timestamps are ISO 8601. serviceDefId is required and must be positive; the item produces an event-sourced occurrence.";

export function registerProgramItemsCreate(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("program_items_create", async (ctx) => {
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
    const parts = arg.split("|").map((p) => p.trim());
    if (parts.length !== 6) {
      await ctx.reply(USAGE);
      return;
    }

    const [programIdStr, seqStr, title, scheduledStart, scheduledEnd, serviceDefStr] = parts;

    const programId = Number(programIdStr);
    if (!Number.isInteger(programId) || programId <= 0) {
      await ctx.reply("programId must be a positive integer.");
      return;
    }
    const sequenceOrder = Number(seqStr);
    if (!Number.isInteger(sequenceOrder) || sequenceOrder < 0) {
      await ctx.reply("seq must be a non-negative integer.");
      return;
    }
    if (title === "") {
      await ctx.reply("title is required.");
      return;
    }
    if (scheduledStart === "" || scheduledEnd === "") {
      await ctx.reply("scheduledStart and scheduledEnd are required (ISO 8601).");
      return;
    }
    const serviceDefId = Number(serviceDefStr);
    if (!Number.isInteger(serviceDefId) || serviceDefId <= 0) {
      await ctx.reply("serviceDefId is required and must be a positive integer.");
      return;
    }

    const result = await api.createProgramItem(programId, {
      sequenceOrder, title, scheduledStart, scheduledEnd,
      serviceDefId, actingAdminId,
    });

    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    await ctx.reply(
      `Program item created: ${result.data.title} (id ${result.data.itemId}, ` +
      `program ${result.data.programId}, sequence ${result.data.sequenceOrder}, ` +
      `service def ${result.data.serviceDefId}).`,
    );
  });
}
