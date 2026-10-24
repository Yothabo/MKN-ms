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

  async listRoles(): Promise<Role[]> {
    const response = await fetch(`${this.baseUrl}/api/config/1.0/roles`);
    if (!response.ok) {
      throw new Error(
        `GET /api/config/1.0/roles returned HTTP ${response.status} ${response.statusText}`,
      );
    }
    return (await response.json()) as Role[];
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
    const asRecord = parsed as Record<string, unknown> | undefined;
    const errField = asRecord?.["error"] as
      | { code?: unknown; message?: unknown }
      | undefined;
    const code =
      typeof errField?.code === "string" ? errField.code : "http_" + String(response.status);
    const message =
      typeof errField?.message === "string"
        ? errField.message
        : response.statusText || "Request failed.";
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
 * Returns the value if it contains no newline characters, null otherwise.
 * Telegram command handlers receive the raw message body after the command
 * word. A multi-line paste — the operator typing two commands in one message,
 * or forwarding a block — would land all of it in ctx.match, and each
 * handler would store the whole block as if it were a single field. Every
 * handler that takes a name-like field calls this; a null response means
 * "reject the command, do not write a corrupt row."
 */
export function singleLine(value: string): string | null {
  if (value.includes("\n") || value.includes("\r")) {
    return null;
  }
  return value;
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
