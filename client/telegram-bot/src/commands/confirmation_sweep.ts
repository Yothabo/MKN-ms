import type { Bot } from "grammy";
import { ApiClient, renderFailure, singleLine } from "../api-client.js";

export function registerConfirmationSweep(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("confirmation_sweep", async (ctx) => {
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

    // No arguments. Reject trailing input rather than silently ignoring it.
    if (text !== undefined && text.indexOf(" ") >= 0) {
      await ctx.reply("Usage: /confirmation_sweep");
      return;
    }

    const result = await api.confirmationSweep();
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }

    const r = result.data;
    const lines = [
      `Confirmation sweep: ${r.status}.`,
      `Assignments transitioned: ${r.assignmentsTransitioned}.`,
      `Replacements created: ${r.replacementsCreated}.`,
      `Slots left vacant: ${r.slotsLeftVacant}.`,
    ];
    if (r.errorDetail) {
      lines.push(`Detail: ${r.errorDetail.slice(0, 300)}`);
    }
    await ctx.reply(lines.join("\n"));
  });
}
