import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerIdentifierIssue(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("identifier_issue", async (ctx) => {
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
    if (parts.length !== 4) {
      await ctx.reply(
        "Usage: /identifier_issue <memberId>|<type>|<number>|<assignedDate>\n" +
        "Example: /identifier_issue 1|Card|C-0001|2026-10-25\n" +
        "Type is free text ('Card', 'Receipt', or any administrator-defined category).",
      );
      return;
    }

    const [memberIdStr, type, number, assignedDate] = parts;

    const memberId = Number(memberIdStr);
    if (!Number.isInteger(memberId) || memberId <= 0) {
      await ctx.reply("memberId must be a positive integer.");
      return;
    }
    if (type === "") {
      await ctx.reply("type is required.");
      return;
    }
    if (number === "") {
      await ctx.reply("number is required.");
      return;
    }
    if (assignedDate === "") {
      await ctx.reply("assignedDate is required (yyyy-mm-dd).");
      return;
    }

    const result = await api.issueIdentifier(
      memberId, type, number, assignedDate, actingAdminId,
    );
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(
      `Identifier issued: ${result.data.type} ${result.data.number} ` +
      `(entry ${result.data.entryId}, member ${result.data.memberId}, ` +
      `assigned ${result.data.assignedDate}).`,
    );
  });
}
