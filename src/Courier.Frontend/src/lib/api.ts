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
