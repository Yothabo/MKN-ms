import type { Bot } from "grammy";
import { ApiClient, renderFailure, singleLine, commandArgument } from "../api-client.js";

const USAGE =
  "Usage: /programs_create <eventId>|<title>\n" +
  "Example: /programs_create 1|Main Program";

export function registerProgramsCreate(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("programs_create", async (ctx) => {
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
    const parts = arg.split("|").map((p) => p.trim());
    if (parts.length !== 2) {
      await ctx.reply(USAGE);
      return;
    }

    const [eventIdStr, title] = parts;
    const eventId = Number(eventIdStr);
    if (!Number.isInteger(eventId) || eventId <= 0) {
      await ctx.reply("eventId must be a positive integer.");
      return;
    }
    if (title === "") {
      await ctx.reply("title is required.");
      return;
    }

    const result = await api.createProgram(eventId, title);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    await ctx.reply(
      `Program created: ${result.data.title} (id ${result.data.programId}, ` +
      `event ${result.data.eventId}).`,
    );
  });
}
