import type { Bot } from "grammy";
import type { ApiClient } from "../api-client.js";

export function registerPing(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("ping", async (ctx) => {
    const userId = ctx.from?.id;
    if (userId === undefined || !isAllowed(userId)) {
      console.warn(
        `/ping rejected: Telegram userId ${userId ?? "<unknown>"} is not in MKN_ADMIN_MAP`,
      );
      await ctx.reply("You are not on the operator allowlist.");
      return;
    }

    try {
      const root = await api.getRoot();
      await ctx.reply(
        `${root.service} is ${root.status}. v${root.version}.`,
      );
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Ping failed: ${message}`);
    }
  });
}
