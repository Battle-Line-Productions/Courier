import type {
  ApiResponse,
  PagedApiResponse,
  JobDto,
  JobStepDto,
  JobExecutionDto,
  BrowseResult,
  CreateJobRequest,
  UpdateJobRequest,
  ReplaceJobStepsRequest,
  TriggerJobRequest,
  ConnectionDto,
  CreateConnectionRequest,
  UpdateConnectionRequest,
  PgpKeyDto,
  GeneratePgpKeyRequest,
  UpdatePgpKeyRequest,
  SshKeyDto,
  GenerateSshKeyRequest,
  UpdateSshKeyRequest,
} from "./types";

const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

class ApiClient {
  private baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  private async request<T>(path: string, options?: RequestInit): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      headers: { "Content-Type": "application/json", ...options?.headers },
      ...options,
    });

    const body = await response.json();

    if (!response.ok && !body.error) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    if (body.error) {
      throw new ApiClientError(body.error);
    }

    return body;
  }

  // Jobs
  async listJobs(page = 1, pageSize = 10): Promise<PagedApiResponse<JobDto>> {
    return this.request(`/api/v1/jobs?page=${page}&pageSize=${pageSize}`);
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

  // Connections
  async listConnections(
    page = 1,
    pageSize = 10,
    filters?: { search?: string; protocol?: string; group?: string; status?: string }
  ): Promise<PagedApiResponse<ConnectionDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.search) params.set("search", filters.search);
    if (filters?.protocol) params.set("protocol", filters.protocol);
    if (filters?.group) params.set("group", filters.group);
    if (filters?.status) params.set("status", filters.status);
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

  async testConnection(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/connections/${id}/test`, { method: "POST" });
  }

  // PGP Keys
  async listPgpKeys(
    page = 1,
    pageSize = 10,
    filters?: { search?: string; status?: string; keyType?: string; algorithm?: string }
  ): Promise<PagedApiResponse<PgpKeyDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.search) params.set("search", filters.search);
    if (filters?.status) params.set("status", filters.status);
    if (filters?.keyType) params.set("keyType", filters.keyType);
    if (filters?.algorithm) params.set("algorithm", filters.algorithm);
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
    const response = await fetch(`${this.baseUrl}/api/v1/pgp-keys/import`, {
      method: "POST",
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
    const response = await fetch(`${this.baseUrl}/api/v1/pgp-keys/${id}/export/public`);
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
    filters?: { search?: string; status?: string; keyType?: string }
  ): Promise<PagedApiResponse<SshKeyDto>> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (filters?.search) params.set("search", filters.search);
    if (filters?.status) params.set("status", filters.status);
    if (filters?.keyType) params.set("keyType", filters.keyType);
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
    const response = await fetch(`${this.baseUrl}/api/v1/ssh-keys/import`, {
      method: "POST",
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
    const response = await fetch(`${this.baseUrl}/api/v1/ssh-keys/${id}/export/public`);
    if (!response.ok) throw new Error(`Export failed: ${response.statusText}`);
    return response.blob();
  }

  async retireSshKey(id: string): Promise<ApiResponse<SshKeyDto>> {
    return this.request(`/api/v1/ssh-keys/${id}/retire`, { method: "POST" });
  }

  async activateSshKey(id: string): Promise<ApiResponse<SshKeyDto>> {
    return this.request(`/api/v1/ssh-keys/${id}/activate`, { method: "POST" });
  }

  // Filesystem
  async browseFilesystem(path?: string): Promise<ApiResponse<BrowseResult>> {
    const params = path ? `?path=${encodeURIComponent(path)}` : "";
    return this.request(`/api/v1/filesystem/browse${params}`);
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
