import type { Bot } from "grammy";
import { ApiClient, singleLine, commandArgument } from "../api-client.js";

const MAX_ROWS = 20;

export function registerEligibilityList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("eligibility_list", async (ctx) => {
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
    const memberId = Number(arg);
    if (!Number.isInteger(memberId) || memberId <= 0) {
      await ctx.reply("Usage: /eligibility_list <memberId>");
      return;
    }

    try {
      const rows = await api.listEligibility(memberId);
      if (rows.length === 0) {
        await ctx.reply(`No eligibility rows for member ${memberId}.`);
        return;
      }
      const textRows = rows.slice(0, MAX_ROWS).map(
        (e) => `• id ${e.eligibilityId} duty ${e.dutyId} — ` +
               `granted ${e.grantedDate}` +
               (e.revokedDate !== null ? `, revoked ${e.revokedDate}` : ""),
      );
      const suffix = rows.length > MAX_ROWS
        ? `\n… and ${rows.length - MAX_ROWS} more.` : "";
      await ctx.reply(`Eligibility for ${memberId}:\n${textRows.join("\n")}${suffix}`);
    } catch (err) {
      const m = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Request failed: ${m.slice(0, 300)}`);
    }
  });
}
