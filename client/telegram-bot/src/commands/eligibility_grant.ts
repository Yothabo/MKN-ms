import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerEligibilityGrant(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("eligibility_grant", async (ctx) => {
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
        "Usage: /eligibility_grant <memberId> <dutyId> <grantedDate>\n" +
        "Example: /eligibility_grant 1 1 2026-01-01",
      );
      return;
    }

    const memberId = Number(parts[0]);
    const dutyId = Number(parts[1]);
    const grantedDate = parts[2];

    if (!Number.isInteger(memberId) || memberId <= 0) {
      await ctx.reply("memberId must be a positive integer.");
      return;
    }
    if (!Number.isInteger(dutyId) || dutyId <= 0) {
      await ctx.reply("dutyId must be a positive integer.");
      return;
    }

    const result = await api.grantEligibility(
      memberId, dutyId, grantedDate, actingAdminId);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(
      `Eligibility granted: id ${result.data.eligibilityId} ` +
      `(member ${result.data.memberId}, duty ${result.data.dutyId}, ` +
      `granted ${result.data.grantedDate}).`,
    );
  });
}
