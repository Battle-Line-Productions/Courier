// API envelope types — mirror backend ApiResponse<T>
export interface ApiError {
  code: number;
  systemMessage: string;
  message: string;
  details?: FieldError[];
}

export interface FieldError {
  field: string;
  message: string;
}

export interface ApiResponse<T> {
  data?: T;
  error?: ApiError;
  timestamp: string;
  success: boolean;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PagedApiResponse<T> {
  data: T[];
  pagination: PaginationMeta;
  error?: ApiError;
  timestamp: string;
  success: boolean;
}

// Domain DTOs
export interface JobDto {
  id: string;
  name: string;
  description?: string;
  currentVersion: number;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface JobStepDto {
  id: string;
  jobId: string;
  stepOrder: number;
  name: string;
  typeKey: string;
  configuration: string;
  timeoutSeconds: number;
}

export interface JobExecutionDto {
  id: string;
  jobId: string;
  state: string;
  triggeredBy: string;
  queuedAt?: string;
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
}

export interface StepExecutionDto {
  id: string;
  jobExecutionId: string;
  jobStepId: string;
  stepOrder: number;
  state: string;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  bytesProcessed?: number;
  outputData?: string;
  errorMessage?: string;
  retryAttempt: number;
  createdAt: string;
}

// Filesystem
export interface FileEntry {
  name: string;
  type: "file" | "directory";
  size?: number;
  lastModified?: string;
}

export interface BrowseResult {
  currentPath: string;
  parentPath?: string;
  entries: FileEntry[];
}

// Request types
export interface CreateJobRequest {
  name: string;
  description?: string;
}

export interface UpdateJobRequest {
  name: string;
  description?: string;
}

export interface StepInput {
  name: string;
  typeKey: string;
  stepOrder: number;
  configuration: string;
  timeoutSeconds: number;
}

export interface ReplaceJobStepsRequest {
  steps: StepInput[];
}

export interface TriggerJobRequest {
  triggeredBy: string;
}
