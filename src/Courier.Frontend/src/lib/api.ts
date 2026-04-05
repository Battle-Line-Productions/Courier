import type {
  ApiResponse,
  PagedApiResponse,
  JobDto,
  JobStepDto,
  JobExecutionDto,
  JobScheduleDto,
  BrowseResult,
  CreateJobRequest,
  UpdateJobRequest,
  ReplaceJobStepsRequest,
  TriggerJobRequest,
  CreateJobScheduleRequest,
  UpdateJobScheduleRequest,
  ConnectionDto,
  ConnectionTestDto,
  CreateConnectionRequest,
  UpdateConnectionRequest,
  MonitorDto,
  MonitorFileLogDto,
  CreateMonitorRequest,
  UpdateMonitorRequest,
  PgpKeyDto,
  GeneratePgpKeyRequest,
  UpdatePgpKeyRequest,
  SshKeyDto,
  GenerateSshKeyRequest,
  UpdateSshKeyRequest,
  DashboardSummaryDto,
  RecentExecutionDto,
  ExpiringKeyDto,
  AuditLogEntryDto,
  TagDto,
  TagEntityDto,
  CreateTagRequest,
  UpdateTagRequest,
  BulkTagAssignmentRequest,
  JobChainDto,
  ChainExecutionDto,
  CreateChainRequest,
  UpdateChainRequest,
  ReplaceChainMembersRequest,
  TriggerChainRequest,
  ChainScheduleDto,
  CreateChainScheduleRequest,
  UpdateChainScheduleRequest,
  JobDependencyDto,
  AddJobDependencyRequest,
  NotificationRuleDto,
  CreateNotificationRuleRequest,
  UpdateNotificationRuleRequest,
  NotificationLogDto,
  LoginRequest,
  LoginResponse,
  RefreshRequest,
  UserProfileDto,
  ChangePasswordRequest,
  SetupStatusDto,
  InitializeSetupRequest,
  UserDto,
  CreateUserRequest,
  UpdateUserRequest,
  NewPasswordRequest,
  AuthSettingsDto,
  UpdateAuthSettingsRequest,
  SmtpSettingsDto,
  UpdateSmtpSettingsRequest,
  SmtpTestResult,
  StepTypeMetadataDto,
  FeedbackItemDto,
  CreateFeedbackRequest,
  FeedbackVoteResponse,
  GitHubOAuthUrlResponse,
  GitHubLinkResponse,
  AuthProviderDto,
  LoginOptionDto,
  CreateAuthProviderRequest,
  UpdateAuthProviderRequest,
  TestConnectionResult,
  SsoExchangeResponse,
} from "./types";

const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

class ApiClient {
  private baseUrl: string;
  private accessToken: string | null = null;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  setAccessToken(token: string | null) {
    this.accessToken = token;
  }

  private async request<T>(path: string, options?: RequestInit): Promise<T> {
    const headers: Record<string, string> = {};
    if (this.accessToken) {
      headers["Authorization"] = `Bearer ${this.accessToken}`;
    }
    if (options?.body) {
      headers["Content-Type"] = "application/json";
    }

    const response = await fetch(`${this.baseUrl}${path}`, {
      ...options,
      headers: { ...headers, ...options?.headers },
    });

    const body = await response.json();

    if (response.status === 401 && !body.error) {
      throw new ApiClientError({
        code: 10007,
        systemMessage: "Unauthorized",
        message: "Your session has expired. Please log in again.",
      });
    }

    if (!response.ok && !body.error) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    if (body.error) {
      throw new ApiClientError(body.error);
    }

    return body;
  }

  // Jobs
  async listJobs(page = 1, pageSize = 10, filters?: { tag?: string }): Promise<PagedApiResponse<JobDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.tag) params.set("tag", filters.tag);
    return this.request(`/api/v1/jobs?${params}`);
  }

  async getJob(id: string): Promise<ApiResponse<JobDto>> {
    return this.request(`/api/v1/jobs/${id}`);
  }

  async createJob(data: CreateJobRequest): Promise<ApiResponse<JobDto>> {
    return this.request("/api/v1/jobs", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateJob(id: string, data: UpdateJobRequest): Promise<ApiResponse<JobDto>> {
    return this.request(`/api/v1/jobs/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteJob(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/jobs/${id}`, { method: "DELETE" });
  }

  // Step Types
  async listStepTypes(): Promise<ApiResponse<StepTypeMetadataDto[]>> {
    return this.request("/api/v1/step-types");
  }

  // Steps
  async listSteps(jobId: string): Promise<ApiResponse<JobStepDto[]>> {
    return this.request(`/api/v1/jobs/${jobId}/steps`);
  }

  async replaceSteps(jobId: string, data: ReplaceJobStepsRequest): Promise<ApiResponse<JobStepDto[]>> {
    return this.request(`/api/v1/jobs/${jobId}/steps`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  // Executions
  async listExecutions(jobId: string, page = 1, pageSize = 10): Promise<PagedApiResponse<JobExecutionDto>> {
    return this.request(`/api/v1/jobs/${jobId}/executions?page=${page}&pageSize=${pageSize}`);
  }

  async getExecution(executionId: string): Promise<ApiResponse<JobExecutionDto>> {
    return this.request(`/api/v1/jobs/executions/${executionId}`);
  }

  async triggerJob(jobId: string, data: TriggerJobRequest = { triggeredBy: "ui" }): Promise<ApiResponse<JobExecutionDto>> {
    return this.request(`/api/v1/jobs/${jobId}/trigger`, {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async pauseExecution(executionId: string): Promise<ApiResponse<JobExecutionDto>> {
    return this.request(`/api/v1/jobs/executions/${executionId}/pause`, {
      method: "POST",
    });
  }

  async resumeExecution(executionId: string): Promise<ApiResponse<JobExecutionDto>> {
    return this.request(`/api/v1/jobs/executions/${executionId}/resume`, {
      method: "POST",
    });
  }

  async cancelExecution(executionId: string, reason?: string): Promise<ApiResponse<JobExecutionDto>> {
    return this.request(`/api/v1/jobs/executions/${executionId}/cancel`, {
      method: "POST",
      body: JSON.stringify({ reason }),
    });
  }

  // Schedules
  async listSchedules(jobId: string): Promise<ApiResponse<JobScheduleDto[]>> {
    return this.request(`/api/v1/jobs/${jobId}/schedules`);
  }

  async createSchedule(jobId: string, data: CreateJobScheduleRequest): Promise<ApiResponse<JobScheduleDto>> {
    return this.request(`/api/v1/jobs/${jobId}/schedules`, {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateSchedule(jobId: string, scheduleId: string, data: UpdateJobScheduleRequest): Promise<ApiResponse<JobScheduleDto>> {
    return this.request(`/api/v1/jobs/${jobId}/schedules/${scheduleId}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteSchedule(jobId: string, scheduleId: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/jobs/${jobId}/schedules/${scheduleId}`, { method: "DELETE" });
  }

  // Connections
  async listConnections(
    page = 1,
    pageSize = 10,
    filters?: { search?: string; protocol?: string; group?: string; status?: string; tag?: string }
  ): Promise<PagedApiResponse<ConnectionDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.search) params.set("search", filters.search);
    if (filters?.protocol) params.set("protocol", filters.protocol);
    if (filters?.group) params.set("group", filters.group);
    if (filters?.status) params.set("status", filters.status);
    if (filters?.tag) params.set("tag", filters.tag);
    return this.request(`/api/v1/connections?${params}`);
  }

  async getConnection(id: string): Promise<ApiResponse<ConnectionDto>> {
    return this.request(`/api/v1/connections/${id}`);
  }

  async createConnection(data: CreateConnectionRequest): Promise<ApiResponse<ConnectionDto>> {
    return this.request("/api/v1/connections", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateConnection(id: string, data: UpdateConnectionRequest): Promise<ApiResponse<ConnectionDto>> {
    return this.request(`/api/v1/connections/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteConnection(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/connections/${id}`, { method: "DELETE" });
  }

  async testConnection(id: string): Promise<ApiResponse<ConnectionTestDto>> {
    return this.request(`/api/v1/connections/${id}/test`, { method: "POST" });
  }

  // PGP Keys
  async listPgpKeys(
    page = 1,
    pageSize = 10,
    filters?: { search?: string; status?: string; keyType?: string; algorithm?: string; tag?: string }
  ): Promise<PagedApiResponse<PgpKeyDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.search) params.set("search", filters.search);
    if (filters?.status) params.set("status", filters.status);
    if (filters?.keyType) params.set("keyType", filters.keyType);
    if (filters?.algorithm) params.set("algorithm", filters.algorithm);
    if (filters?.tag) params.set("tag", filters.tag);
    return this.request(`/api/v1/pgp-keys?${params}`);
  }

  async getPgpKey(id: string): Promise<ApiResponse<PgpKeyDto>> {
    return this.request(`/api/v1/pgp-keys/${id}`);
  }

  async generatePgpKey(data: GeneratePgpKeyRequest): Promise<ApiResponse<PgpKeyDto>> {
    return this.request("/api/v1/pgp-keys/generate", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async importPgpKey(formData: FormData): Promise<ApiResponse<PgpKeyDto>> {
    const headers: Record<string, string> = {};
    if (this.accessToken) {
      headers["Authorization"] = `Bearer ${this.accessToken}`;
    }
    const response = await fetch(`${this.baseUrl}/api/v1/pgp-keys/import`, {
      method: "POST",
      headers,
      body: formData,
    });
    const body = await response.json();
    if (body.error) throw new ApiClientError(body.error);
    return body;
  }

  async updatePgpKey(id: string, data: UpdatePgpKeyRequest): Promise<ApiResponse<PgpKeyDto>> {
    return this.request(`/api/v1/pgp-keys/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deletePgpKey(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/pgp-keys/${id}`, { method: "DELETE" });
  }

  async exportPgpPublicKey(id: string): Promise<Blob> {
    const headers: Record<string, string> = {};
    if (this.accessToken) {
      headers["Authorization"] = `Bearer ${this.accessToken}`;
    }
    const response = await fetch(`${this.baseUrl}/api/v1/pgp-keys/${id}/export/public`, {
      headers,
    });
    if (!response.ok) throw new Error(`Export failed: ${response.statusText}`);
    return response.blob();
  }

  async retirePgpKey(id: string): Promise<ApiResponse<PgpKeyDto>> {
    return this.request(`/api/v1/pgp-keys/${id}/retire`, { method: "POST" });
  }

  async revokePgpKey(id: string): Promise<ApiResponse<PgpKeyDto>> {
    return this.request(`/api/v1/pgp-keys/${id}/revoke`, { method: "POST" });
  }

  async activatePgpKey(id: string): Promise<ApiResponse<PgpKeyDto>> {
    return this.request(`/api/v1/pgp-keys/${id}/activate`, { method: "POST" });
  }

  // SSH Keys
  async listSshKeys(
    page = 1,
    pageSize = 10,
    filters?: { search?: string; status?: string; keyType?: string; tag?: string }
  ): Promise<PagedApiResponse<SshKeyDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.search) params.set("search", filters.search);
    if (filters?.status) params.set("status", filters.status);
    if (filters?.keyType) params.set("keyType", filters.keyType);
    if (filters?.tag) params.set("tag", filters.tag);
    return this.request(`/api/v1/ssh-keys?${params}`);
  }

  async getSshKey(id: string): Promise<ApiResponse<SshKeyDto>> {
    return this.request(`/api/v1/ssh-keys/${id}`);
  }

  async generateSshKey(data: GenerateSshKeyRequest): Promise<ApiResponse<SshKeyDto>> {
    return this.request("/api/v1/ssh-keys/generate", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async importSshKey(formData: FormData): Promise<ApiResponse<SshKeyDto>> {
    const headers: Record<string, string> = {};
    if (this.accessToken) {
      headers["Authorization"] = `Bearer ${this.accessToken}`;
    }
    const response = await fetch(`${this.baseUrl}/api/v1/ssh-keys/import`, {
      method: "POST",
      headers,
      body: formData,
    });
    const body = await response.json();
    if (body.error) throw new ApiClientError(body.error);
    return body;
  }

  async updateSshKey(id: string, data: UpdateSshKeyRequest): Promise<ApiResponse<SshKeyDto>> {
    return this.request(`/api/v1/ssh-keys/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteSshKey(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/ssh-keys/${id}`, { method: "DELETE" });
  }

  async exportSshPublicKey(id: string): Promise<Blob> {
    const headers: Record<string, string> = {};
    if (this.accessToken) {
      headers["Authorization"] = `Bearer ${this.accessToken}`;
    }
    const response = await fetch(`${this.baseUrl}/api/v1/ssh-keys/${id}/export/public`, {
      headers,
    });
    if (!response.ok) throw new Error(`Export failed: ${response.statusText}`);
    return response.blob();
  }

  async retireSshKey(id: string): Promise<ApiResponse<SshKeyDto>> {
    return this.request(`/api/v1/ssh-keys/${id}/retire`, { method: "POST" });
  }

  async activateSshKey(id: string): Promise<ApiResponse<SshKeyDto>> {
    return this.request(`/api/v1/ssh-keys/${id}/activate`, { method: "POST" });
  }

  // Monitors
  async listMonitors(
    page = 1,
    pageSize = 10,
    filters?: { search?: string; state?: string; tag?: string }
  ): Promise<PagedApiResponse<MonitorDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.search) params.set("search", filters.search);
    if (filters?.state) params.set("state", filters.state);
    if (filters?.tag) params.set("tag", filters.tag);
    return this.request(`/api/v1/monitors?${params}`);
  }

  async getMonitor(id: string): Promise<ApiResponse<MonitorDto>> {
    return this.request(`/api/v1/monitors/${id}`);
  }

  async createMonitor(data: CreateMonitorRequest): Promise<ApiResponse<MonitorDto>> {
    return this.request("/api/v1/monitors", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateMonitor(id: string, data: UpdateMonitorRequest): Promise<ApiResponse<MonitorDto>> {
    return this.request(`/api/v1/monitors/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteMonitor(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/monitors/${id}`, { method: "DELETE" });
  }

  async activateMonitor(id: string): Promise<ApiResponse<MonitorDto>> {
    return this.request(`/api/v1/monitors/${id}/activate`, { method: "POST" });
  }

  async pauseMonitor(id: string): Promise<ApiResponse<MonitorDto>> {
    return this.request(`/api/v1/monitors/${id}/pause`, { method: "POST" });
  }

  async disableMonitor(id: string): Promise<ApiResponse<MonitorDto>> {
    return this.request(`/api/v1/monitors/${id}/disable`, { method: "POST" });
  }

  async acknowledgeMonitorError(id: string): Promise<ApiResponse<MonitorDto>> {
    return this.request(`/api/v1/monitors/${id}/acknowledge-error`, { method: "POST" });
  }

  async listMonitorFileLog(
    monitorId: string,
    page = 1,
    pageSize = 25
  ): Promise<PagedApiResponse<MonitorFileLogDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    return this.request(`/api/v1/monitors/${monitorId}/file-log?${params}`);
  }

  // Dashboard
  async getDashboardSummary(): Promise<ApiResponse<DashboardSummaryDto>> {
    return this.request("/api/v1/dashboard/summary");
  }

  async getRecentExecutions(count = 10): Promise<ApiResponse<RecentExecutionDto[]>> {
    return this.request(`/api/v1/dashboard/recent-executions?count=${count}`);
  }

  async getActiveMonitors(): Promise<ApiResponse<MonitorDto[]>> {
    return this.request("/api/v1/dashboard/active-monitors");
  }

  async getExpiringKeys(daysAhead = 30): Promise<ApiResponse<ExpiringKeyDto[]>> {
    return this.request(`/api/v1/dashboard/key-expiry?daysAhead=${daysAhead}`);
  }

  // Filesystem
  async browseFilesystem(path?: string): Promise<ApiResponse<BrowseResult>> {
    const params = path ? `?path=${encodeURIComponent(path)}` : "";
    return this.request(`/api/v1/filesystem/browse${params}`);
  }

  // Audit Log
  async listAuditLog(
    page = 1,
    pageSize = 25,
    filters?: { entityType?: string; operation?: string; performedBy?: string; from?: string; to?: string }
  ): Promise<PagedApiResponse<AuditLogEntryDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.entityType) params.set("entityType", filters.entityType);
    if (filters?.operation) params.set("operation", filters.operation);
    if (filters?.performedBy) params.set("performedBy", filters.performedBy);
    if (filters?.from) params.set("from", filters.from);
    if (filters?.to) params.set("to", filters.to);
    return this.request(`/api/v1/audit-log?${params}`);
  }

  async listAuditLogByEntity(
    entityType: string,
    entityId: string,
    page = 1,
    pageSize = 25
  ): Promise<PagedApiResponse<AuditLogEntryDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    return this.request(`/api/v1/audit-log/entity/${entityType}/${entityId}?${params}`);
  }

  // Tags
  async listTags(params?: { page?: number; pageSize?: number; search?: string; category?: string }): Promise<PagedApiResponse<TagDto>> {
    const searchParams = new URLSearchParams();
    if (params?.page) searchParams.set("page", String(params.page));
    if (params?.pageSize) searchParams.set("pageSize", String(params.pageSize));
    if (params?.search) searchParams.set("search", params.search);
    if (params?.category) searchParams.set("category", params.category);
    return this.request(`/api/v1/tags?${searchParams}`);
  }

  async getTag(id: string): Promise<ApiResponse<TagDto>> {
    return this.request(`/api/v1/tags/${id}`);
  }

  async createTag(data: CreateTagRequest): Promise<ApiResponse<TagDto>> {
    return this.request("/api/v1/tags", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateTag(id: string, data: UpdateTagRequest): Promise<ApiResponse<TagDto>> {
    return this.request(`/api/v1/tags/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteTag(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/tags/${id}`, { method: "DELETE" });
  }

  async listTagEntities(id: string, params?: { entityType?: string; page?: number; pageSize?: number }): Promise<PagedApiResponse<TagEntityDto>> {
    const searchParams = new URLSearchParams();
    if (params?.entityType) searchParams.set("entityType", params.entityType);
    if (params?.page) searchParams.set("page", String(params.page));
    if (params?.pageSize) searchParams.set("pageSize", String(params.pageSize));
    return this.request(`/api/v1/tags/${id}/entities?${searchParams}`);
  }

  async assignTags(data: BulkTagAssignmentRequest): Promise<ApiResponse<void>> {
    return this.request("/api/v1/tags/assign", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async unassignTags(data: BulkTagAssignmentRequest): Promise<ApiResponse<void>> {
    return this.request("/api/v1/tags/unassign", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  // Chains
  async listChains(page = 1, pageSize = 10): Promise<PagedApiResponse<JobChainDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    return this.request(`/api/v1/chains?${params}`);
  }

  async getChain(id: string): Promise<ApiResponse<JobChainDto>> {
    return this.request(`/api/v1/chains/${id}`);
  }

  async createChain(data: CreateChainRequest): Promise<ApiResponse<JobChainDto>> {
    return this.request("/api/v1/chains", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateChain(id: string, data: UpdateChainRequest): Promise<ApiResponse<JobChainDto>> {
    return this.request(`/api/v1/chains/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteChain(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/chains/${id}`, { method: "DELETE" });
  }

  async setChainEnabled(id: string, enabled: boolean): Promise<ApiResponse<JobChainDto>> {
    return this.request(`/api/v1/chains/${id}`, {
      method: "PUT",
      body: JSON.stringify({ name: "", isEnabled: enabled }),
    });
  }

  async replaceChainMembers(chainId: string, data: ReplaceChainMembersRequest): Promise<ApiResponse<JobChainDto>> {
    return this.request(`/api/v1/chains/${chainId}/members`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async triggerChain(chainId: string, data: TriggerChainRequest = { triggeredBy: "ui" }): Promise<ApiResponse<ChainExecutionDto>> {
    return this.request(`/api/v1/chains/${chainId}/execute`, {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async listChainExecutions(chainId: string, page = 1, pageSize = 10): Promise<PagedApiResponse<ChainExecutionDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    return this.request(`/api/v1/chains/${chainId}/executions?${params}`);
  }

  async getChainExecution(chainId: string, executionId: string): Promise<ApiResponse<ChainExecutionDto>> {
    return this.request(`/api/v1/chains/${chainId}/executions/${executionId}`);
  }

  // Chain Schedules
  async listChainSchedules(chainId: string): Promise<ApiResponse<ChainScheduleDto[]>> {
    return this.request(`/api/v1/chains/${chainId}/schedules`);
  }

  async createChainSchedule(chainId: string, data: CreateChainScheduleRequest): Promise<ApiResponse<ChainScheduleDto>> {
    return this.request(`/api/v1/chains/${chainId}/schedules`, {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateChainSchedule(chainId: string, scheduleId: string, data: UpdateChainScheduleRequest): Promise<ApiResponse<ChainScheduleDto>> {
    return this.request(`/api/v1/chains/${chainId}/schedules/${scheduleId}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteChainSchedule(chainId: string, scheduleId: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/chains/${chainId}/schedules/${scheduleId}`, { method: "DELETE" });
  }

  // Job Dependencies
  async listJobDependencies(jobId: string): Promise<ApiResponse<JobDependencyDto[]>> {
    return this.request(`/api/v1/jobs/${jobId}/dependencies`);
  }

  async addJobDependency(jobId: string, data: AddJobDependencyRequest): Promise<ApiResponse<JobDependencyDto>> {
    return this.request(`/api/v1/jobs/${jobId}/dependencies`, {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async removeJobDependency(jobId: string, dependencyId: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/jobs/${jobId}/dependencies/${dependencyId}`, { method: "DELETE" });
  }

  // Notification Rules
  async listNotificationRules(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    entityType?: string;
    channel?: string;
    isEnabled?: boolean;
  }): Promise<PagedApiResponse<NotificationRuleDto>> {
    const searchParams = new URLSearchParams();
    if (params?.page) searchParams.set("page", params.page.toString());
    if (params?.pageSize) searchParams.set("pageSize", params.pageSize.toString());
    if (params?.search) searchParams.set("search", params.search);
    if (params?.entityType) searchParams.set("entityType", params.entityType);
    if (params?.channel) searchParams.set("channel", params.channel);
    if (params?.isEnabled !== undefined) searchParams.set("isEnabled", params.isEnabled.toString());
    const qs = searchParams.toString();
    return this.request(`/api/v1/notification-rules${qs ? `?${qs}` : ""}`);
  }

  async getNotificationRule(id: string): Promise<ApiResponse<NotificationRuleDto>> {
    return this.request(`/api/v1/notification-rules/${id}`);
  }

  async createNotificationRule(data: CreateNotificationRuleRequest): Promise<ApiResponse<NotificationRuleDto>> {
    return this.request("/api/v1/notification-rules", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
  }

  async updateNotificationRule(id: string, data: UpdateNotificationRuleRequest): Promise<ApiResponse<NotificationRuleDto>> {
    return this.request(`/api/v1/notification-rules/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(data),
    });
  }

  async deleteNotificationRule(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/notification-rules/${id}`, { method: "DELETE" });
  }

  async testNotificationRule(id: string): Promise<ApiResponse<{ sent: boolean }>> {
    return this.request(`/api/v1/notification-rules/${id}/test`, { method: "POST" });
  }

  // Notification Logs
  async listNotificationLogs(params?: {
    page?: number;
    pageSize?: number;
    ruleId?: string;
    entityType?: string;
    entityId?: string;
    success?: boolean;
  }): Promise<PagedApiResponse<NotificationLogDto>> {
    const searchParams = new URLSearchParams();
    if (params?.page) searchParams.set("page", params.page.toString());
    if (params?.pageSize) searchParams.set("pageSize", params.pageSize.toString());
    if (params?.ruleId) searchParams.set("ruleId", params.ruleId);
    if (params?.entityType) searchParams.set("entityType", params.entityType);
    if (params?.entityId) searchParams.set("entityId", params.entityId);
    if (params?.success !== undefined) searchParams.set("success", params.success.toString());
    const qs = searchParams.toString();
    return this.request(`/api/v1/notification-logs${qs ? `?${qs}` : ""}`);
  }

  // Auth
  async login(data: LoginRequest): Promise<ApiResponse<LoginResponse>> {
    return this.request("/api/v1/auth/login", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async refreshToken(data: RefreshRequest): Promise<ApiResponse<LoginResponse>> {
    return this.request("/api/v1/auth/refresh", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async logout(refreshToken: string): Promise<ApiResponse<void>> {
    return this.request("/api/v1/auth/logout", {
      method: "POST",
      body: JSON.stringify({ refreshToken }),
    });
  }

  async getMe(): Promise<ApiResponse<UserProfileDto>> {
    return this.request("/api/v1/auth/me");
  }

  async changePassword(data: ChangePasswordRequest): Promise<ApiResponse<void>> {
    return this.request("/api/v1/auth/change-password", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  // Setup
  async getSetupStatus(): Promise<ApiResponse<SetupStatusDto>> {
    return this.request("/api/v1/setup/status");
  }

  async initializeSetup(data: InitializeSetupRequest): Promise<ApiResponse<UserProfileDto>> {
    return this.request("/api/v1/setup/initialize", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  // Users (admin)
  async listUsers(page = 1, pageSize = 10, search?: string): Promise<PagedApiResponse<UserDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (search) params.set("search", search);
    return this.request(`/api/v1/users?${params}`);
  }

  async getUser(id: string): Promise<ApiResponse<UserDto>> {
    return this.request(`/api/v1/users/${id}`);
  }

  async createUser(data: CreateUserRequest): Promise<ApiResponse<UserDto>> {
    return this.request("/api/v1/users", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateUser(id: string, data: UpdateUserRequest): Promise<ApiResponse<UserDto>> {
    return this.request(`/api/v1/users/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteUser(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/users/${id}`, { method: "DELETE" });
  }

  async resetUserPassword(id: string, data: NewPasswordRequest): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/users/${id}/reset-password`, {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  // Settings
  async getAuthSettings(): Promise<ApiResponse<AuthSettingsDto>> {
    return this.request("/api/v1/settings/auth");
  }

  async updateAuthSettings(data: UpdateAuthSettingsRequest): Promise<ApiResponse<AuthSettingsDto>> {
    return this.request("/api/v1/settings/auth", {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async getSmtpSettings(): Promise<ApiResponse<SmtpSettingsDto>> {
    return this.request("/api/v1/settings/smtp");
  }

  async updateSmtpSettings(data: UpdateSmtpSettingsRequest): Promise<ApiResponse<SmtpSettingsDto>> {
    return this.request("/api/v1/settings/smtp", {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async testSmtpConnection(): Promise<ApiResponse<SmtpTestResult>> {
    return this.request("/api/v1/settings/smtp/test", {
      method: "POST",
    });
  }

  // Feedback
  async listFeedback(params: { type?: string; page?: number; pageSize?: number; state?: string }): Promise<ApiResponse<FeedbackItemDto[]>> {
    const searchParams = new URLSearchParams();
    if (params.type) searchParams.set("type", params.type);
    if (params.page) searchParams.set("page", String(params.page));
    if (params.pageSize) searchParams.set("pageSize", String(params.pageSize));
    if (params.state) searchParams.set("state", params.state);
    const qs = searchParams.toString();
    return this.request(`/api/v1/feedback${qs ? `?${qs}` : ""}`);
  }

  async getFeedbackItem(number: number): Promise<ApiResponse<FeedbackItemDto>> {
    return this.request(`/api/v1/feedback/${number}`);
  }

  async createFeedback(data: CreateFeedbackRequest): Promise<ApiResponse<FeedbackItemDto>> {
    return this.request("/api/v1/feedback", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async voteFeedback(number: number): Promise<ApiResponse<FeedbackVoteResponse>> {
    return this.request(`/api/v1/feedback/${number}/vote`, { method: "POST" });
  }

  async unvoteFeedback(number: number): Promise<ApiResponse<FeedbackVoteResponse>> {
    return this.request(`/api/v1/feedback/${number}/vote`, { method: "DELETE" });
  }

  // GitHub OAuth
  async getGitHubAuthUrl(): Promise<ApiResponse<GitHubOAuthUrlResponse>> {
    return this.request("/api/v1/auth/github/authorize");
  }

  async linkGitHubAccount(code: string): Promise<ApiResponse<GitHubLinkResponse>> {
    return this.request("/api/v1/auth/github/callback", {
      method: "POST",
      body: JSON.stringify({ code }),
    });
  }

  async unlinkGitHubAccount(): Promise<ApiResponse<void>> {
    return this.request("/api/v1/auth/github/unlink", { method: "DELETE" });
  }

  // Auth Providers
  async listAuthProviders(page = 1, pageSize = 25): Promise<PagedApiResponse<AuthProviderDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    return this.request(`/api/v1/auth-providers?${params}`);
  }

  async getAuthProvider(id: string): Promise<ApiResponse<AuthProviderDto>> {
    return this.request(`/api/v1/auth-providers/${id}`);
  }

  async createAuthProvider(data: CreateAuthProviderRequest): Promise<ApiResponse<AuthProviderDto>> {
    return this.request("/api/v1/auth-providers", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateAuthProvider(id: string, data: UpdateAuthProviderRequest): Promise<ApiResponse<AuthProviderDto>> {
    return this.request(`/api/v1/auth-providers/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteAuthProvider(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/auth-providers/${id}`, { method: "DELETE" });
  }

  async testAuthProvider(id: string): Promise<ApiResponse<TestConnectionResult>> {
    return this.request(`/api/v1/auth-providers/${id}/test`, { method: "POST" });
  }

  async getLoginOptions(): Promise<ApiResponse<LoginOptionDto[]>> {
    return this.request("/api/v1/auth/login-options");
  }

  async exchangeSsoCode(code: string): Promise<ApiResponse<SsoExchangeResponse>> {
    return this.request("/api/v1/auth/sso/exchange", {
      method: "POST",
      body: JSON.stringify({ code }),
    });
  }
}

export class ApiClientError extends Error {
  code: number;
  systemMessage: string;
  details?: { field: string; message: string }[];

  constructor(error: { code: number; systemMessage: string; message: string; details?: { field: string; message: string }[] }) {
    super(error.message);
    this.name = "ApiClientError";
    this.code = error.code;
    this.systemMessage = error.systemMessage;
    this.details = error.details;
  }
}

export const api = new ApiClient(API_URL);
