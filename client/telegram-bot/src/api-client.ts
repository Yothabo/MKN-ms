export interface RootResponse {
  service: string;
  status: string;
  version: string;
}

export interface Role {
  roleId: number;
  name: string;
  isDefault: boolean;
  isActive: boolean;
  isDeleted: boolean;
}

export interface Duty {
  dutyId: number;
  name: string;
  isActive: boolean;
  isDeleted: boolean;
}

export interface Branch {
  branchId: number;
  name: string;
  location: string;
  usesAttendanceRegister: boolean | null;
  isActive: boolean;
  isDeleted: boolean;
}

export interface Member {
  memberId: number;
  name: string;
  surname: string;
  joinDate: string;
  dateOfBirth: string;
  membershipStage: string;
  gender: string;
  phone: string;
  email: string | null;
  branchId: number;
  roleId: number;
  memberStatusId: number;
}

export interface CreateMemberBody {
  name: string;
  surname: string;
  joinDate: string;
  dateOfBirth: string;
  membershipStage: string;
  gender: string;
  phone: string;
  branchId: number;
  roleId: number;
  memberStatusId: number;
  actingAdminId: number;
}

export interface ServiceOccurrence {
  occurrenceId: number;
  scheduleId: number | null;
  eventId: number | null;
  date: string;
  serviceTypeId: number | null;
  startTime: string | null;
  fillStatusId: number | null;
  generatedBy: string;
  createdBy: number | null;
  changedBy: number | null;
}

export interface ApiFailure {
  ok: false;
  status: number;
  code: string;
  message: string;
}

export type ApiResult<T> = { ok: true; data: T } | ApiFailure;

export class ApiClient {
  constructor(private readonly baseUrl: string) {}

  async getRoot(): Promise<RootResponse> {
    const response = await fetch(`${this.baseUrl}/`);
    if (!response.ok) {
      throw new Error(
        `GET / returned HTTP ${response.status} ${response.statusText}`,
      );
    }
    return (await response.json()) as RootResponse;
  }

  async createRole(name: string, actingAdminId: number): Promise<ApiResult<Role>> {
    return this.postJson<Role>("/api/config/1.0/roles", { name, actingAdminId });
  }

  async createDuty(name: string, actingAdminId: number): Promise<ApiResult<Duty>> {
    return this.postJson<Duty>("/api/config/1.0/duties", { name, actingAdminId });
  }

  async createBranch(name: string, location: string, actingAdminId: number): Promise<ApiResult<Branch>> {
    return this.postJson<Branch>("/api/config/1.0/branches", { name, location, actingAdminId });
  }

  async createMember(body: CreateMemberBody): Promise<ApiResult<Member>> {
    return this.postJson<Member>("/api/config/3.0/members", body);
  }

  async listOccurrences(fromDate?: string, toDate?: string): Promise<ServiceOccurrence[]> {
    const params = new URLSearchParams();
    if (fromDate) params.set("fromDate", fromDate);
    if (toDate) params.set("toDate", toDate);
    const qs = params.toString();
    const url = qs === ""
      ? "/api/config/1.0/occurrences"
      : "/api/config/1.0/occurrences?" + qs;
    return this.getJson<ServiceOccurrence[]>(url);
  }

  async listRoles(): Promise<Role[]> {
    const response = await fetch(`${this.baseUrl}/api/config/1.0/roles`);
    if (!response.ok) {
      throw new Error(
        `GET /api/config/1.0/roles returned HTTP ${response.status} ${response.statusText}`,
      );
    }
    return (await response.json()) as Role[];
  }

  // Shared JSON GET helper. Every list command reuses it.
  private async getJson<T>(path: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`);
    const text = await response.text();
    if (!response.ok) {
      throw new Error(
        `GET ${path} returned HTTP ${response.status} ${response.statusText}: ${text.slice(0, 200)}`,
      );
    }
    return JSON.parse(text) as T;
  }

  // Shared JSON POST helper. Every write command reuses it.
  private async postJson<T>(path: string, body: unknown): Promise<ApiResult<T>> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });

    const text = await response.text();
    let parsed: unknown = undefined;
    try {
      parsed = text === "" ? undefined : JSON.parse(text);
    } catch {
      // Non-JSON body; surface as an unknown error.
      return {
        ok: false,
        status: response.status,
        code: "http_" + String(response.status),
        message: text || response.statusText,
      };
    }

    if (response.ok) {
      return { ok: true, data: parsed as T };
    }

    // The API's failure envelope is { error: { code, message } }.
    //
    // A server 500 in a non-translating case may return a non-JSON body
    // (development exception page) or a very long JSON message. Either
    // way, an unbounded string must never reach a Telegram reply: at
    // 4000+ characters it exceeds Telegram's 4096-character limit and
    // the reply fails, masking the original error. Cap the message at
    // 300 characters and log the full body to the bot's terminal, where
    // the operator can read it.
    const asRecord = parsed as Record<string, unknown> | undefined;
    const errField = asRecord?.["error"] as
      | { code?: unknown; message?: unknown }
      | undefined;
    const code =
      typeof errField?.code === "string" ? errField.code : "http_" + String(response.status);

    const rawMessage =
      typeof errField?.message === "string"
        ? errField.message
        : response.statusText || "Request failed.";

    let message: string;
    if (rawMessage.length > 300) {
      message =
        rawMessage.slice(0, 300) +
        " … (truncated; full body in bot terminal)";
      console.error(
        `postJson: ${path} returned an oversized message ` +
          `(${rawMessage.length} chars); truncated to 300. Full body:`,
        rawMessage,
      );
    } else {
      message = rawMessage;
    }

    return { ok: false, status: response.status, code, message };
  }
}

const OPERATOR_HINTS: Record<string, (m: string) => string> = {
  configuration_missing: (m) =>
    `A required setting is not configured. Seed it before retrying. (${m})`,
  configuration_invalid: (m) =>
    `A required setting has an invalid value. Correct it. (${m})`,
  validation_failed: (m) => `The request is malformed: ${m}`,
  not_found: (m) => `Not found: ${m}`,
  duplicate_name: (m) => `Already exists: ${m}`,
  already_inactive: (m) => `Already inactive: ${m}`,
  already_active: (m) => `Already active: ${m}`,
  referenced_entity_missing: (m) => `Referenced entity does not exist: ${m}`,
  referenced_entity_inactive: (m) =>
    `Referenced entity is inactive or soft-deleted: ${m}`,
  lifecycle_violation: (m) => `Operation not permitted: ${m}`,
};

/**
 * Enforces the bot's one-line-per-message rule.
 *
 * Takes the raw message text as Telegram delivered it (ctx.message.text),
 * not ctx.match. grammY consumes the separator between the command word
 * and its argument, so a message of "/roles_create\nThirdTest" arrives
 * with ctx.match = "ThirdTest" and no newline visible on that field. The
 * newline is only in the full text. Checking ctx.match silently accepted
 * multi-line pastes; checking ctx.message.text rejects them.
 *
 * Returns null if the message contains a newline anywhere. Otherwise
 * returns the message text trimmed of leading and trailing whitespace.
 */
export function singleLine(rawMessageText: string | undefined): string | null {
  if (rawMessageText === undefined) {
    return null;
  }
  if (rawMessageText.includes("\n") || rawMessageText.includes("\r")) {
    return null;
  }
  return rawMessageText.trim();
}

/**
 * Extracts the argument part of a command line given the raw message text
 * and the command word. The command line is "\command arg". Returns the
 * text after the command word and a single separating space, trimmed.
 */
export function commandArgument(rawMessageText: string | undefined): string {
  if (rawMessageText === undefined) {
    return "";
  }
  const space = rawMessageText.indexOf(" ");
  if (space < 0) {
    return "";
  }
  return rawMessageText.slice(space + 1).trim();
}

export function renderFailure(failure: ApiFailure): string {
  if (failure.status >= 500) {
    return `API returned a server error (${failure.status}). Check the API log. ${failure.message}`;
  }
  const hint = OPERATOR_HINTS[failure.code];
  if (hint) {
    return hint(failure.message);
  }
  return `${failure.code}: ${failure.message}`;
}
