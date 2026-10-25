import type { Bot } from "grammy";
import { ApiClient, singleLine, commandArgument } from "../api-client.js";

const MAX_ROWS = 20;

export function registerDutyRulesList(
  bot: Bot,
  api: ApiClient,
  isAllowed: (userId: number) => boolean,
): void {
  bot.command("duty_rules_list", async (ctx) => {
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
    const dutyId = Number(arg);
    if (!Number.isInteger(dutyId) || dutyId <= 0) {
      await ctx.reply("Usage: /duty_rules_list <dutyId>");
      return;
    }

    try {
      const rules = await api.listDutyRules(dutyId);
      if (rules.length === 0) {
        await ctx.reply(`No rules for duty ${dutyId}.`);
        return;
      }
      const rows = rules.slice(0, MAX_ROWS).map(
        (r) => `• id ${r.ruleId} tier ${r.tierOrder} — ` +
               `${r.criteriaType}=${r.criteriaValue}` +
               (r.serviceDefId !== null ? ` (def ${r.serviceDefId})` : ""),
      );
      const suffix = rules.length > MAX_ROWS
        ? `\n… and ${rules.length - MAX_ROWS} more.` : "";
      await ctx.reply(`Duty rules for ${dutyId}:\n${rows.join("\n")}${suffix}`);
    } catch (err) {
      const m = err instanceof Error ? err.message : String(err);
      await ctx.reply(`Request failed: ${m.slice(0, 300)}`);
    }
  });
}
