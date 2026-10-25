import type { Bot } from "grammy";
import { ApiClient, singleLine, commandArgument } from "../api-client.js";

const MAX_ROWS = 20;

export function registerMembersList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("members_list", async (ctx) => {
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
    let branchId: number | undefined;
    if (arg !== "") {
      const n = Number(arg);
      if (!Number.isInteger(n) || n <= 0) {
        await ctx.reply("Usage: /members_list [branchId]");
        return;
      }
      branchId = n;
    }

    try {
      const members = await api.listMembers(branchId);
      if (members.length === 0) {
        await ctx.reply("No members.");
        return;
      }
      const rows = members.slice(0, MAX_ROWS).map(
        (m) => `• id ${m.memberId} — ${m.name} ${m.surname} ` +
               `(branch ${m.branchId}, role ${m.roleId}, status ${m.memberStatusId})`,
      );
      const suffix = members.length > MAX_ROWS
        ? `\n… and ${members.length - MAX_ROWS} more.` : "";
      await ctx.reply(`Members:\n${rows.join("\n")}${suffix}`);
    } catch (err) {
      const m = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Request failed: ${m.slice(0, 300)}`);
    }
  });
}
