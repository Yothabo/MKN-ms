import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerMemberSetIdentifier(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("member_set_identifier", async (ctx) => {
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
    if (parts.length !== 3) {
      await ctx.reply(
        "Usage: /member_set_identifier <memberId>|<cardNumber>|<receiptNumber>\n" +
        "Use '-' for either value to leave that column unchanged.\n" +
        "Example: /member_set_identifier 1|C-0001|-",
      );
      return;
    }

    const [memberIdStr, cardArg, receiptArg] = parts;

    const memberId = Number(memberIdStr);
    if (!Number.isInteger(memberId) || memberId <= 0) {
      await ctx.reply("memberId must be a positive integer.");
      return;
    }

    const cardNumber = cardArg === "-" ? null : cardArg;
    const receiptNumber = receiptArg === "-" ? null : receiptArg;

    if (cardNumber === null && receiptNumber === null) {
      await ctx.reply("At least one of cardNumber or receiptNumber must be supplied (not '-').");
      return;
    }

    const result = await api.setMemberIdentifier(
      memberId, cardNumber, receiptNumber, actingAdminId);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(
      `Member ${result.data.memberId} updated: ` +
      `card=${result.data.cardNumber ?? "(none)"}, ` +
      `receipt=${result.data.receiptNumber ?? "(none)"}.`,
    );
  });
}
