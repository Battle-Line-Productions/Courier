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
  stepExecutions?: StepExecutionDto[];
}

export interface StepExecutionDto {
  id: string;
  jobExecutionId?: string;
  jobStepId?: string;
  stepOrder: number;
  stepName: string;
  stepTypeKey: string;
  state: string;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  bytesProcessed?: number;
  outputData?: string;
  errorMessage?: string;
  retryAttempt: number;
  createdAt?: string;
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

// Schedules
export interface JobScheduleDto {
  id: string;
  jobId: string;
  scheduleType: string;
  cronExpression?: string;
  runAt?: string;
  isEnabled: boolean;
  lastFiredAt?: string;
  nextFireAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateJobScheduleRequest {
  scheduleType: string;
  cronExpression?: string;
  runAt?: string;
  isEnabled: boolean;
}

export interface UpdateJobScheduleRequest {
  cronExpression?: string;
  runAt?: string;
  isEnabled?: boolean;
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

// Connection types
export interface ConnectionDto {
  id: string;
  name: string;
  group?: string;
  protocol: string;
  host: string;
  port: number;
  authMethod: string;
  username: string;
  hasPassword: boolean;
  hasClientSecret: boolean;
  sshKeyId?: string;
  properties?: string;
  hostKeyPolicy: string;
  storedHostFingerprint?: string;
  sshAlgorithms?: string;
  passiveMode: boolean;
  tlsVersionFloor?: string;
  tlsCertPolicy: string;
  tlsPinnedThumbprint?: string;
  connectTimeoutSec: number;
  operationTimeoutSec: number;
  keepaliveIntervalSec: number;
  transportRetries: number;
  status: string;
  fipsOverride: boolean;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateConnectionRequest {
  name: string;
  group?: string;
  protocol: string;
  host: string;
  port?: number;
  authMethod: string;
  username: string;
  password?: string;
  clientSecret?: string;
  sshKeyId?: string;
  properties?: string;
  hostKeyPolicy?: string;
  sshAlgorithms?: string;
  passiveMode?: boolean;
  tlsVersionFloor?: string;
  tlsCertPolicy?: string;
  tlsPinnedThumbprint?: string;
  connectTimeoutSec?: number;
  operationTimeoutSec?: number;
  keepaliveIntervalSec?: number;
  transportRetries?: number;
  fipsOverride?: boolean;
  notes?: string;
}

export interface UpdateConnectionRequest {
  name: string;
  group?: string;
  protocol: string;
  host: string;
  port?: number;
  authMethod: string;
  username: string;
  password?: string;
  clientSecret?: string;
  sshKeyId?: string;
  properties?: string;
  hostKeyPolicy?: string;
  sshAlgorithms?: string;
  passiveMode?: boolean;
  tlsVersionFloor?: string;
  tlsCertPolicy?: string;
  tlsPinnedThumbprint?: string;
  connectTimeoutSec?: number;
  operationTimeoutSec?: number;
  keepaliveIntervalSec?: number;
  transportRetries?: number;
  status?: string;
  fipsOverride?: boolean;
  notes?: string;
}

// Azure Function types
export interface AzureFunctionTraceDto {
  timestamp: string;
  message: string;
  severityLevel: number;
}

// PGP Key types
export interface PgpKeyDto {
  id: string;
  name: string;
  fingerprint?: string;
  shortKeyId?: string;
  algorithm: string;
  keyType: string;
  purpose?: string;
  status: string;
  hasPublicKey: boolean;
  hasPrivateKey: boolean;
  expiresAt?: string;
  successorKeyId?: string;
  createdBy?: string;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

export interface GeneratePgpKeyRequest {
  name: string;
  algorithm: string;
  purpose?: string;
  passphrase?: string;
  realName?: string;
  email?: string;
  expiresInDays?: number;
}

export interface ImportPgpKeyRequest {
  name: string;
  purpose?: string;
  passphrase?: string;
}

export interface UpdatePgpKeyRequest {
  name?: string;
  purpose?: string;
  notes?: string;
}

// SSH Key types
export interface SshKeyDto {
  id: string;
  name: string;
  keyType: string;
  fingerprint?: string;
  status: string;
  hasPublicKey: boolean;
  hasPrivateKey: boolean;
  notes?: string;
  createdBy?: string;
  createdAt: string;
  updatedAt: string;
}

export interface GenerateSshKeyRequest {
  name: string;
  keyType: string;
  passphrase?: string;
  notes?: string;
}

export interface ImportSshKeyRequest {
  name: string;
  passphrase?: string;
}

export interface UpdateSshKeyRequest {
  name?: string;
  notes?: string;
}
