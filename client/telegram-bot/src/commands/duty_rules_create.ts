import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerDutyRulesCreate(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("duty_rules_create", async (ctx) => {
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
    if (parts.length < 4 || parts.length > 4) {
      await ctx.reply(
        "Usage: /duty_rules_create <dutyId> <tierOrder> <criteriaType> <criteriaValue>\n" +
        "Example: /duty_rules_create 1 1 Role Coordinator\n" +
        "criteriaType is one of: Role, Gender, AgeRange, Tenure, " +
        "MembershipStage, BranchAttendanceRecency, EligibilityFlag, " +
        "MemberAttribute, MemberStatus, Youth. Values with spaces must " +
        "use an underscore: MemberAttribute Purity=Pure",
      );
      return;
    }

    const dutyId = Number(parts[0]);
    const tierOrder = Number(parts[1]);
    const criteriaType = parts[2];
    const criteriaValue = parts[3].replace(/_/g, " ");

    if (!Number.isInteger(dutyId) || dutyId <= 0) {
      await ctx.reply("dutyId must be a positive integer.");
      return;
    }
    if (!Number.isInteger(tierOrder) || tierOrder <= 0) {
      await ctx.reply("tierOrder must be a positive integer.");
      return;
    }

    const result = await api.createDutyRule(
      dutyId,
      tierOrder,
      criteriaType,
      criteriaValue,
      actingAdminId,
    );
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(
      `Duty rule created: id ${result.data.ruleId}, tier ${result.data.tierOrder}, ` +
      `${result.data.criteriaType}=${result.data.criteriaValue}.`,
    );
  });
}
