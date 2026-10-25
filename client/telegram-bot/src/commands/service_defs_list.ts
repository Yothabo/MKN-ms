import type { Bot } from "grammy";
import { ApiClient, singleLine } from "../api-client.js";

const MAX_ROWS = 20;

export function registerServiceDefsList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("service_defs_list", async (ctx) => {
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
      const defs = await api.listServiceDefinitions();
      if (defs.length === 0) {
        await ctx.reply("No service definitions.");
        return;
      }
      const rows = defs.slice(0, MAX_ROWS).map(
        (d) => `• id ${d.serviceDefId} — ${d.name} ` +
               `(type ${d.serviceTypeId}, owning ${d.owningBranchId ?? "global"})`,
      );
      const suffix = defs.length > MAX_ROWS
        ? `\n… and ${defs.length - MAX_ROWS} more.` : "";
      await ctx.reply(`Service definitions:\n${rows.join("\n")}${suffix}`);
    } catch (err) {
      const m = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Request failed: ${m.slice(0, 300)}`);
    }
  });
}
