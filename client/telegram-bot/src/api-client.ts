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
  // The two columns §13.1.1 reads for attendance-register eligibility.
  // Settable via POST /api/config/3.0/members/{id}/set-identifier
  // (§15.8.41). Absent or null = not currently issued.
  cardNumber: string | null;
  receiptNumber: string | null;
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

export interface ServiceDefinition {
  serviceDefId: number;
  name: string;
  serviceTypeId: number;
  owningBranchId: number | null;
  isActive: boolean;
  isDeleted: boolean;
}

export interface ServiceSchedule {
  scheduleId: number;
  serviceDefId: number;
  timeSlotId: number;
  startTime: string;
  isActive: boolean;
  isDeleted: boolean;
}

export interface DutyRule {
  ruleId: number;
  dutyId: number;
  serviceDefId: number | null;
  tierOrder: number;
  criteriaType: string;
  criteriaValue: string;
  isActive: boolean;
  isDeleted: boolean;
}

export interface Eligibility {
  eligibilityId: number;
  memberId: number;
  dutyId: number;
  grantedDate: string;
  grantedBy: number;
  revokedDate: string | null;
  revokedReason: string | null;
}

export interface ServiceDefinitionCreated {
  serviceDefId: number;
  name: string;
  serviceTypeId: number;
  owningBranchId: number | null;
  isActive: boolean;
  isDeleted: boolean;
}

export interface ServiceScheduleCreated {
  scheduleId: number;
  serviceDefId: number;
  timeSlotId: number;
  startTime: string;
  isActive: boolean;
  isDeleted: boolean;
}

export interface DutyRuleCreated {
  ruleId: number;
  dutyId: number;
  serviceDefId: number | null;
  tierOrder: number;
  criteriaType: string;
  criteriaValue: string;
  isActive: boolean;
  isDeleted: boolean;
}

export interface EligibilityCreated {
  eligibilityId: number;
  memberId: number;
  dutyId: number;
  grantedDate: string;
  grantedBy: number;
  revokedDate: string | null;
  revokedReason: string | null;
}

export interface MaterializeResult {
  runId: number;
  status: string;
  schedulesEvaluated: number;
  occurrencesCreated: number;
  errorDetail: string | null;
}

export interface GenerateAssignmentResult {
  status: string;
  occurrencesProcessed: number;
  assignmentsCreated: number;
  dutiesFullyFilled: number;
  dutiesPartiallyFilled: number;
  dutiesUnfilled: number;
  errorDetail: string | null;
}

export interface RecordAttendanceResult {
  status: string;
  recorded: boolean;
  recordId: number;
  errorDetail: string | null;
}

export interface IdentifierHistory {
  entryId: number;
  memberId: number;
  type: string;
  number: string;
  assignedDate: string;
  unassignedDate: string | null;
  reason: string | null;
  authorizedBy: number;
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

  async createDutyRule(
    dutyId: number,
    tierOrder: number,
    criteriaType: string,
    criteriaValue: string,
    actingAdminId: number,
  ): Promise<ApiResult<DutyRuleCreated>> {
    return this.postJson<DutyRuleCreated>(
      `/api/config/2.0/duties/${dutyId}/rules`,
      { tierOrder, criteriaType, criteriaValue, actingAdminId },
    );
  }

  async grantEligibility(
    memberId: number,
    dutyId: number,
    grantedDate: string,
    grantedByAdminId: number,
  ): Promise<ApiResult<EligibilityCreated>> {
    return this.postJson<EligibilityCreated>(
      `/api/config/4.0/members/${memberId}/eligibility`,
      { dutyId, grantedDate, grantedByAdminId },
    );
  }

  async createServiceDefinition(
    name: string,
    serviceTypeId: number,
    owningBranchId: number | null,
    actingAdminId: number,
  ): Promise<ApiResult<ServiceDefinitionCreated>> {
    return this.postJson<ServiceDefinitionCreated>(
      "/api/config/1.0/service-definitions",
      { name, serviceTypeId, owningBranchId, actingAdminId },
    );
  }

  async createServiceSchedule(
    serviceDefId: number,
    timeSlotId: number,
    startTime: string,
    actingAdminId: number,
  ): Promise<ApiResult<ServiceScheduleCreated>> {
    return this.postJson<ServiceScheduleCreated>(
      "/api/config/1.0/service-schedules",
      { serviceDefId, timeSlotId, startTime, actingAdminId },
    );
  }

  async materializeManual(
    actingAdminId: number,
    horizonDaysOverride?: number,
  ): Promise<ApiResult<MaterializeResult>> {
    const body: Record<string, unknown> = {
      triggerType: "Manual",
      triggeredByAdminId: actingAdminId,
    };
    if (horizonDaysOverride !== undefined) {
      body.horizonDaysOverride = horizonDaysOverride;
    }
    return this.postJson<MaterializeResult>("/api/processes/11.0/materialize", body);
  }

  async generateAssignment(
    occurrenceId?: number,
    fromDate?: string,
    toDate?: string,
  ): Promise<ApiResult<GenerateAssignmentResult>> {
    const body: Record<string, unknown> = {};
    if (occurrenceId !== undefined) body.occurrenceId = occurrenceId;
    if (fromDate !== undefined) body.fromDate = fromDate;
    if (toDate !== undefined) body.toDate = toDate;
    return this.postJson<GenerateAssignmentResult>(
      "/api/processes/5.0/generate", body);
  }

  async recordAttendance(
    memberId: number,
    occurrenceId: number,
  ): Promise<ApiResult<RecordAttendanceResult>> {
    return this.postJson<RecordAttendanceResult>(
      "/api/processes/6.0/record",
      { memberId, occurrenceId },
    );
  }

  async setMemberIdentifier(
    memberId: number,
    cardNumber: string | null,
    receiptNumber: string | null,
    actingAdminId: number,
  ): Promise<ApiResult<Member>> {
    const body: Record<string, unknown> = { actingAdminId };
    if (cardNumber !== null) body.cardNumber = cardNumber;
    if (receiptNumber !== null) body.receiptNumber = receiptNumber;
    return this.postJson<Member>(
      `/api/config/3.0/members/${memberId}/set-identifier`,
      body,
    );
  }

  async issueIdentifier(
    memberId: number,
    type: string,
    number: string,
    assignedDate: string,
    authorizedByAdminId: number,
  ): Promise<ApiResult<IdentifierHistory>> {
    return this.postJson<IdentifierHistory>(
      `/api/config/3.0/members/${memberId}/identifiers`,
      { type, number, assignedDate, authorizedByAdminId },
    );
  }

  async listDuties(): Promise<Duty[]> {
    return this.getJson<Duty[]>("/api/config/1.0/duties");
  }

  async listBranches(): Promise<Branch[]> {
    return this.getJson<Branch[]>("/api/config/1.0/branches");
  }

  async listMembers(branchId?: number): Promise<Member[]> {
    const url = branchId !== undefined
      ? `/api/config/3.0/members?branchId=${branchId}`
      : "/api/config/3.0/members";
    return this.getJson<Member[]>(url);
  }

  async listServiceDefinitions(): Promise<ServiceDefinition[]> {
    return this.getJson<ServiceDefinition[]>("/api/config/1.0/service-definitions");
  }

  async listServiceSchedules(serviceDefId?: number): Promise<ServiceSchedule[]> {
    const url = serviceDefId !== undefined
      ? `/api/config/1.0/service-schedules?serviceDefId=${serviceDefId}`
      : "/api/config/1.0/service-schedules";
    return this.getJson<ServiceSchedule[]>(url);
  }

  async listDutyRules(dutyId: number): Promise<DutyRule[]> {
    return this.getJson<DutyRule[]>(`/api/config/2.0/duties/${dutyId}/rules`);
  }

  async listEligibility(memberId: number): Promise<Eligibility[]> {
    return this.getJson<Eligibility[]>(
      `/api/config/4.0/members/${memberId}/eligibility`);
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

    // The API uses two failure envelopes.
    //
    // Config endpoints (1.0, 2.0, 3.0, 4.0, 8.0) return:
    //   { "error": { "code": "...", "message": "..." } }
    //
    // Process endpoints (5.0, 6.0, 7.0, 10.0, 11.0, 12.0, 13.0) return
    // their own result envelope with status "Failure" and a free-text
    // errorDetail:
    //   { "status": "Failure", ...processFields, "errorDetail": "..." }
    //
    // The bot must render both. Without this, a process failure
    // surfaces as a bare "http_422: Unprocessable Entity" and the
    // errorDetail — the actual diagnosis — is invisible to the operator.
    //
    // A server 500 may return a non-JSON body (development exception
    // page) or a very long message. Either way, an unbounded string
    // must never reach a Telegram reply: at 4000+ chars it exceeds
    // Telegram's 4096 limit and the reply fails, masking the error.
    // Cap at 300 and log the full body to the terminal.
    const asRecord = parsed as Record<string, unknown> | undefined;

    const errField = asRecord?.["error"] as
      | { code?: unknown; message?: unknown }
      | undefined;

    const configShape =
      typeof errField?.code === "string" || typeof errField?.message === "string";
    const processShape =
      typeof asRecord?.["errorDetail"] === "string"
        && asRecord?.["status"] === "Failure";

    let code: string;
    let rawMessage: string;

    if (configShape) {
      code =
        typeof errField?.code === "string"
          ? errField.code
          : "http_" + String(response.status);
      rawMessage =
        typeof errField?.message === "string"
          ? errField.message
          : response.statusText || "Request failed.";
    } else if (processShape) {
      code = "process_failure";
      rawMessage = asRecord!["errorDetail"] as string;
    } else {
      code = "http_" + String(response.status);
      rawMessage = response.statusText || "Request failed.";
    }

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
  // Process endpoints carry their diagnosis in errorDetail, which the
  // postJson adapter has already put into failure.message. Surface it
  // verbatim: the process's message is already operator-language.
  if (failure.code === "process_failure") {
    return failure.message;
  }
  if (failure.status >= 500) {
    return `API returned a server error (${failure.status}). Check the API log. ${failure.message}`;
  }
  const hint = OPERATOR_HINTS[failure.code];
  if (hint) {
    return hint(failure.message);
  }
  return `${failure.code}: ${failure.message}`;
}
