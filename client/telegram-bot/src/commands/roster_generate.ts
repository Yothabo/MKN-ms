import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerRosterGenerate(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("roster_generate", async (ctx) => {
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

    // Two forms:
    //   /roster_generate <occurrenceId>
    //   /roster_generate <fromDate> <toDate>
    let occurrenceId: number | undefined;
    let fromDate: string | undefined;
    let toDate: string | undefined;

    if (parts.length === 1) {
      const n = Number(parts[0]);
      if (!Number.isInteger(n) || n <= 0) {
        await ctx.reply(
          "Usage:\n" +
          "  /roster_generate <occurrenceId>\n" +
          "  /roster_generate <fromDate> <toDate>",
        );
        return;
      }
      occurrenceId = n;
    } else if (parts.length === 2) {
      fromDate = parts[0];
      toDate = parts[1];
    } else {
      await ctx.reply(
        "Usage:\n" +
        "  /roster_generate <occurrenceId>\n" +
        "  /roster_generate <fromDate> <toDate>",
      );
      return;
    }

    const result = await api.generateAssignment(occurrenceId, fromDate, toDate);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    const r = result.data;
    const lines = [
      `Generate assignment: ${r.status}.`,
      `Occurrences processed: ${r.occurrencesProcessed}.`,
      `Assignments created: ${r.assignmentsCreated}.`,
      `Duties fully filled: ${r.dutiesFullyFilled}.`,
      `Duties partially filled: ${r.dutiesPartiallyFilled}.`,
      `Duties unfilled: ${r.dutiesUnfilled}.`,
    ];
    if (r.errorDetail) {
      lines.push(`Detail: ${r.errorDetail.slice(0, 300)}`);
    }
    await ctx.reply(lines.join("\n"));
  });
}
