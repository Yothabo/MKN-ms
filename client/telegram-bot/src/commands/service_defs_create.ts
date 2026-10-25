import type { Bot } from "grammy";
import {
  ApiClient,
  renderFailure,
  singleLine,
  commandArgument,
} from "../api-client.js";

export function registerServiceDefsCreate(
  bot: Bot,
  api: ApiClient,
  resolveAdminId: (userId: number) => number | undefined,
): void {
  bot.command("service_defs_create", async (ctx) => {
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

    // <name>|<serviceTypeId>|<owningBranchId|global>
    const arg = commandArgument(text);
    if (arg === "") {
      await ctx.reply(
        "Usage: /service_defs_create <name>|<serviceTypeId>|<owningBranchId|global>\n" +
        "Example: /service_defs_create Sunday Service|1|global\n" +
        "Example: /service_defs_create Main Sunday|1|1",
      );
      return;
    }

    const parts = arg.split("|").map((p) => p.trim());
    if (parts.length !== 3) {
      await ctx.reply(
        "Expected 3 pipe-separated fields: <name>|<serviceTypeId>|<owningBranchId|global>",
      );
      return;
    }

    const [name, serviceTypeIdStr, owningBranchStr] = parts;
    const serviceTypeId = Number(serviceTypeIdStr);
    if (!Number.isInteger(serviceTypeId) || serviceTypeId <= 0) {
      await ctx.reply("serviceTypeId must be a positive integer.");
      return;
    }

    let owningBranchId: number | null;
    if (owningBranchStr === "global" || owningBranchStr === "") {
      owningBranchId = null;
    } else {
      const n = Number(owningBranchStr);
      if (!Number.isInteger(n) || n <= 0) {
        await ctx.reply("owningBranchId must be a positive integer, or 'global'.");
        return;
      }
      owningBranchId = n;
    }

    const result = await api.createServiceDefinition(
      name, serviceTypeId, owningBranchId, actingAdminId);
    if (!result.ok) {
      await ctx.reply(renderFailure(result));
      return;
    }
    await ctx.reply(
      `Service definition created: ${result.data.name} ` +
      `(id ${result.data.serviceDefId}, ` +
      `owning ${result.data.owningBranchId ?? "global"}).`,
    );
  });
}
