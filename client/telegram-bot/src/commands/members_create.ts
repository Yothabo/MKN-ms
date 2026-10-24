import type { Bot } from "grammy";
import { ApiClient, renderFailure, singleLine, commandArgument } from "../api-client.js";

const USAGE =
  "Usage: /members_create <name>|<surname>|<joinDate>|<dob>|<stage>|<gender>|<phone>|<branchId>|<roleId>|<statusId>\n" +
  "Dates are yyyy-mm-dd. Example:\n" +
  "/members_create Alex|Example|2024-01-15|1990-05-20|Full|Other|+2700000|1|1|1";

export function registerMembersCreate(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("members_create", async (ctx) => {
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
    if (arg === "") {
      await ctx.reply(USAGE);
      return;
    }

    const parts = arg.split("|").map((p) => p.trim());
    if (parts.length !== 10) {
      await ctx.reply(
        `Expected 10 pipe-separated fields, got ${parts.length}.\n${USAGE}`,
      );
      return;
    }

    const [name, surname, joinDate, dob, stage, gender, phone, branchIdStr, roleIdStr, statusIdStr] = parts;

    const branchId = Number(branchIdStr);
    const roleId = Number(roleIdStr);
    const memberStatusId = Number(statusIdStr);
    if (!Number.isInteger(branchId) || branchId <= 0) {
      await ctx.reply(`branchId must be a positive integer, got '${branchIdStr}'.`);
      return;
    }
    if (!Number.isInteger(roleId) || roleId <= 0) {
      await ctx.reply(`roleId must be a positive integer, got '${roleIdStr}'.`);
      return;
    }
    if (!Number.isInteger(memberStatusId) || memberStatusId <= 0) {
      await ctx.reply(`statusId must be a positive integer, got '${statusIdStr}'.`);
      return;
    }

    const result = await api.createMember({
      name, surname, joinDate, dateOfBirth: dob,
      membershipStage: stage, gender, phone,
      branchId, roleId, memberStatusId, actingAdminId,
    });

    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(
      `Member created: ${result.data.name} ${result.data.surname} (id ${result.data.memberId}).`,
    );
  });
}
