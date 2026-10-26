import type { Bot } from "grammy";
import { ApiClient, renderFailure, singleLine, commandArgument } from "../api-client.js";

const USAGE =
  "Usage: /events_create <name>|<startDate>|<endDate>|<type>|<hostBranchId>\n" +
  "Example: /events_create Conference|2026-11-01|2026-11-02|Conference|1\n" +
  "Dates are yyyy-mm-dd. Register defaults ON; override via a separate toggle command.";

export function registerEventsCreate(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("events_create", async (ctx) => {
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
    if (parts.length !== 5) {
      await ctx.reply(USAGE);
      return;
    }

    const [name, startDate, endDate, type, hostBranchStr] = parts;
    if (name === "" || startDate === "" || endDate === "" || type === "") {
      await ctx.reply(USAGE);
      return;
    }

    const hostBranchId = Number(hostBranchStr);
    if (!Number.isInteger(hostBranchId) || hostBranchId <= 0) {
      await ctx.reply("hostBranchId must be a positive integer.");
      return;
    }

    const result = await api.createEvent({
      name, startDate, endDate, type, hostBranchId,
      usesAttendanceRegister: true,
    });

    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    await ctx.reply(
      `Event created: ${result.data.name} (id ${result.data.eventId}, ` +
      `register ${result.data.usesAttendanceRegister === true ? "on" : "off"}).`,
    );
  });
}
