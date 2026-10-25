import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerMaterializeManual(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("materialize_manual", async (ctx) => {
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
    let horizonOverride: number | undefined;
    if (arg !== "") {
      const n = Number(arg);
      if (!Number.isInteger(n) || n < 0) {
        await ctx.reply("Usage: /materialize_manual [horizonDays]");
        return;
      }
      horizonOverride = n;
    }

    const result = await api.materializeManual(actingAdminId, horizonOverride);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    const r = result.data;
    const lines = [
      `Materialize run ${r.runId}: ${r.status}.`,
      `Schedules evaluated: ${r.schedulesEvaluated}.`,
      `Occurrences created: ${r.occurrencesCreated}.`,
    ];
    if (r.errorDetail) {
      lines.push(`Detail: ${r.errorDetail.slice(0, 300)}`);
    }
    await ctx.reply(lines.join("\n"));
  });
}
