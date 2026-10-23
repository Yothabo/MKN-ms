import "dotenv/config";

export interface Config {
  apiBaseUrl: string;
  botToken: string;
  adminMap: Map<number, number>;
}

export function loadConfig(): Config {
  const apiBaseUrl = process.env.MKN_API_BASE_URL;
  if (!apiBaseUrl) {
    throw new Error("MKN_API_BASE_URL is not set");
  }

  const botToken = process.env.MKN_BOT_TOKEN;
  if (!botToken) {
    throw new Error("MKN_BOT_TOKEN is not set");
  }

  const rawMap = process.env.MKN_ADMIN_MAP ?? "";
  if (rawMap.trim() === "") {
    throw new Error(
      "MKN_ADMIN_MAP is empty. Set at least one <telegramUserId>:<adminId> pair.",
    );
  }

  const adminMap = new Map<number, number>();
  for (const pair of rawMap.split(",")) {
    const trimmed = pair.trim();
    if (trimmed === "") continue;
    const parts = trimmed.split(":");
    if (parts.length !== 2) {
      throw new Error(
        `MKN_ADMIN_MAP entry '${trimmed}' is malformed; expected <telegramUserId>:<adminId>`,
      );
    }
    const userId = Number(parts[0]);
    const adminId = Number(parts[1]);
    if (!Number.isInteger(userId) || userId <= 0) {
      throw new Error(`MKN_ADMIN_MAP entry '${trimmed}': invalid telegramUserId`);
    }
    if (!Number.isInteger(adminId) || adminId <= 0) {
      throw new Error(`MKN_ADMIN_MAP entry '${trimmed}': invalid adminId`);
    }
    adminMap.set(userId, adminId);
  }

  return { apiBaseUrl, botToken, adminMap };
}
