import { Bot } from "grammy";
import { loadConfig } from "./config.js";
import { ApiClient } from "./api-client.js";
import { registerPing } from "./commands/ping.js";
import { registerRolesCreate } from "./commands/roles_create.js";
import { registerRolesList } from "./commands/roles_list.js";
import { registerDutiesCreate } from "./commands/duties_create.js";
import { registerBranchesCreate } from "./commands/branches_create.js";
import { registerMembersCreate } from "./commands/members_create.js";
import { registerOccurrencesList } from "./commands/occurrences_list.js";
import { registerDutiesList } from "./commands/duties_list.js";
import { registerBranchesList } from "./commands/branches_list.js";
import { registerMembersList } from "./commands/members_list.js";
import { registerServiceDefsList } from "./commands/service_defs_list.js";
import { registerSchedulesList } from "./commands/schedules_list.js";
import { registerDutyRulesList } from "./commands/duty_rules_list.js";
import { registerEligibilityList } from "./commands/eligibility_list.js";
import { registerDutyRulesCreate } from "./commands/duty_rules_create.js";
import { registerEligibilityGrant } from "./commands/eligibility_grant.js";
import { registerServiceDefsCreate } from "./commands/service_defs_create.js";
import { registerServiceDefSchedulesCreate } from "./commands/service_def_schedules_create.js";
import { registerMaterializeManual } from "./commands/materialize_manual.js";
import { registerRosterGenerate } from "./commands/roster_generate.js";
import { registerAttendanceRecord } from "./commands/attendance_record.js";
import { registerIdentifierIssue } from "./commands/identifier_issue.js";
import { registerMemberSetIdentifier } from "./commands/member_set_identifier.js";
import { registerConfirmationSweep } from "./commands/confirmation_sweep.js";
import { registerFillEvaluate } from "./commands/fill_evaluate.js";

async function main(): Promise<void> {
  const config = loadConfig();
  const api = new ApiClient(config.apiBaseUrl);
  const bot = new Bot(config.botToken);

  const isAllowed = (userId: number): boolean =>
    config.adminMap.has(userId);
  const resolveAdminId = (userId: number): number | undefined =>
    config.adminMap.get(userId);

  registerPing(bot, api, isAllowed);
  registerRolesCreate(bot, api, resolveAdminId);
  registerRolesList(bot, api, isAllowed);
  registerDutiesCreate(bot, api, resolveAdminId);
  registerBranchesCreate(bot, api, resolveAdminId);
  registerMembersCreate(bot, api, resolveAdminId);
  registerOccurrencesList(bot, api, isAllowed);
  registerDutiesList(bot, api, isAllowed);
  registerBranchesList(bot, api, isAllowed);
  registerMembersList(bot, api, isAllowed);
  registerServiceDefsList(bot, api, isAllowed);
  registerSchedulesList(bot, api, isAllowed);
  registerDutyRulesList(bot, api, isAllowed);
  registerEligibilityList(bot, api, isAllowed);
  registerDutyRulesCreate(bot, api, resolveAdminId);
  registerEligibilityGrant(bot, api, resolveAdminId);
  registerServiceDefsCreate(bot, api, resolveAdminId);
  registerServiceDefSchedulesCreate(bot, api, resolveAdminId);
  registerMaterializeManual(bot, api, resolveAdminId);
  registerRosterGenerate(bot, api, isAllowed);
  registerAttendanceRecord(bot, api, isAllowed);
  registerIdentifierIssue(bot, api, resolveAdminId);
  registerMemberSetIdentifier(bot, api, resolveAdminId);
  registerConfirmationSweep(bot, api, isAllowed);
  registerFillEvaluate(bot, api, isAllowed);

  bot.catch(async (botErr) => {
    // Full detail goes to the terminal. The chat gets a short message.
    // A stack trace is not useful to an operator and, at 4000+
    // characters, exceeds Telegram's reply limit and causes a second
    // failure that masks the first.
    if (botErr.error instanceof Error) {
      console.error("Bot error:", botErr.error.stack ?? botErr.error.message);
    } else {
      console.error("Bot error:", String(botErr.error));
    }
    try {
      await botErr.ctx.reply(
        "Something went wrong. Check the bot's terminal output for the trace.",
      );
    } catch {
      // Reply failed too; nothing further to do.
    }
  });

  console.log(`MKN-MS bot starting. API at ${config.apiBaseUrl}.`);
  await bot.start();
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
