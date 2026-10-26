import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

const USAGE =
  "Usage:\n" +
  "  /fill_evaluate <occurrenceId>\n" +
  "  /fill_evaluate <fromDate> <toDate>";

export function registerFillEvaluate(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("fill_evaluate", async (ctx) => {
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

    let occurrenceId: number | undefined;
    let fromDate: string | undefined;
    let toDate: string | undefined;

    if (parts.length === 1) {
      const n = Number(parts[0]);
      if (!Number.isInteger(n) || n <= 0) {
        await ctx.reply(USAGE);
        return;
      }
      occurrenceId = n;
    } else if (parts.length === 2) {
      fromDate = parts[0];
      toDate = parts[1];
    } else {
      await ctx.reply(USAGE);
      return;
    }

    const result = await api.evaluateFillStatus(occurrenceId, fromDate, toDate);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    const r = result.data;
    const lines = [
      `Fill status evaluation: ${r.status}.`,
      `Occurrences evaluated: ${r.occurrencesEvaluated}.`,
      `Unfilled: ${r.unfilledCount}.`,
      `Partially filled: ${r.partiallyFilledCount}.`,
      `Filled: ${r.filledCount}.`,
      `Skipped (cancelled): ${r.skippedCancelledCount}.`,
    ];
    if (r.errorDetail) {
      lines.push(`Detail: ${r.errorDetail.slice(0, 300)}`);
    }
    await ctx.reply(lines.join("\n"));
  });
}
