import type { Bot } from "grammy";
import { ApiClient, singleLine } from "../api-client.js";

const MAX_ROWS = 20;

export function registerBranchesList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("branches_list", async (ctx) => {
    const userId = ctx.from?.id;
    if (userId === undefined || !isAllowed(userId)) {
      await ctx.reply("You are not on the operator allowlist.");
      return;
    }

    if (singleLine(ctx.message?.text) === null) {
      await ctx.reply("One command per message. Do not paste multiple lines.");
      return;
    }

    try {
      const branches = await api.listBranches();
      if (branches.length === 0) {
        await ctx.reply("No branches.");
        return;
      }
      const rows = branches.slice(0, MAX_ROWS).map(
        (b) => `• ${b.name} — id ${b.branchId} (${b.location})`,
      );
      const suffix = branches.length > MAX_ROWS
        ? `\n… and ${branches.length - MAX_ROWS} more.` : "";
      await ctx.reply(`Branches:\n${rows.join("\n")}${suffix}`);
    } catch (err) {
      const m = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Request failed: ${m.slice(0, 300)}`);
    }
  });
}
