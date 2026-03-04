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
  tags?: TagSummaryDto[];
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
  pausedAt?: string;
  pausedBy?: string;
  cancelledAt?: string;
  cancelledBy?: string;
  cancelReason?: string;
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
  tags?: TagSummaryDto[];
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

// Connection test types
export interface ConnectionTestDto {
  connected: boolean;
  latencyMs: number;
  serverBanner?: string;
  supportedAlgorithms?: SshAlgorithmDto;
  tlsCertificate?: TlsCertificateDto;
  error?: string;
}

export interface SshAlgorithmDto {
  cipher: string[];
  kex: string[];
  mac: string[];
  hostKey: string[];
}

export interface TlsCertificateDto {
  subject: string;
  issuer: string;
  validFrom: string;
  validTo: string;
  thumbprint: string;
}

// Azure Function types
export interface AzureFunctionTraceDto {
  timestamp: string;
  message: string;
  severityLevel: number;
}

// Monitor types
export interface MonitorDto {
  id: string;
  name: string;
  description?: string;
  watchTarget: string;
  triggerEvents: number;
  filePatterns?: string;
  pollingIntervalSec: number;
  stabilityWindowSec: number;
  batchMode: boolean;
  maxConsecutiveFailures: number;
  consecutiveFailureCount: number;
  state: string;
  lastPolledAt?: string;
  tags?: TagSummaryDto[];
  createdAt: string;
  updatedAt: string;
  bindings: MonitorJobBindingDto[];
}

export interface MonitorJobBindingDto {
  id: string;
  jobId: string;
  jobName?: string;
}

export interface MonitorFileLogDto {
  id: string;
  filePath: string;
  fileSize: number;
  fileHash?: string;
  lastModified: string;
  triggeredAt: string;
  executionId?: string;
}

export interface WatchTarget {
  type: "local" | "remote";
  path: string;
  connectionId?: string;
}

export interface CreateMonitorRequest {
  name: string;
  description?: string;
  watchTarget: string;
  triggerEvents: number;
  filePatterns?: string;
  pollingIntervalSec: number;
  stabilityWindowSec?: number;
  batchMode?: boolean;
  maxConsecutiveFailures?: number;
  jobIds: string[];
}

export interface UpdateMonitorRequest {
  name?: string;
  description?: string;
  watchTarget?: string;
  triggerEvents?: number;
  filePatterns?: string;
  pollingIntervalSec?: number;
  stabilityWindowSec?: number;
  batchMode?: boolean;
  maxConsecutiveFailures?: number;
  jobIds?: string[];
}

// Dashboard types
export interface DashboardSummaryDto {
  totalJobs: number;
  totalConnections: number;
  totalMonitors: number;
  totalPgpKeys: number;
  totalSshKeys: number;
  executions24H: number;
  executionsSucceeded24H: number;
  executionsFailed24H: number;
  executions7D: number;
  executionsSucceeded7D: number;
  executionsFailed7D: number;
}

export interface RecentExecutionDto {
  id: string;
  jobId: string;
  jobName?: string;
  state: string;
  triggeredBy: string;
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
}

export interface ExpiringKeyDto {
  id: string;
  name: string;
  keyType: string;
  fingerprint?: string;
  expiresAt: string;
  daysUntilExpiry: number;
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
  tags?: TagSummaryDto[];
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
  tags?: TagSummaryDto[];
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

// Audit log types
export interface AuditLogEntryDto {
  id: string;
  entityType: string;
  entityId: string;
  operation: string;
  performedBy: string;
  performedAt: string;
  details: string;
}

export interface AuditLogFilter {
  entityType?: string;
  entityId?: string;
  operation?: string;
  performedBy?: string;
  from?: string;
  to?: string;
}

// Tags
export interface TagDto {
  id: string;
  name: string;
  color?: string;
  category?: string;
  description?: string;
  createdAt: string;
  updatedAt: string;
}

export interface TagSummaryDto {
  name: string;
  color?: string;
}

export interface CreateTagRequest {
  name: string;
  color?: string;
  category?: string;
  description?: string;
}

export interface UpdateTagRequest {
  name: string;
  color?: string;
  category?: string;
  description?: string;
}

export interface TagAssignment {
  tagId: string;
  entityType: string;
  entityId: string;
}

export interface BulkTagAssignmentRequest {
  assignments: TagAssignment[];
}

export interface TagEntityDto {
  entityId: string;
  entityType: string;
}

// Chains
export interface JobChainDto {
  id: string;
  name: string;
  description?: string;
  isEnabled: boolean;
  members: JobChainMemberDto[];
  createdAt: string;
  updatedAt: string;
}

export interface JobChainMemberDto {
  id: string;
  jobId: string;
  jobName: string;
  executionOrder: number;
  dependsOnMemberId?: string;
  runOnUpstreamFailure: boolean;
}

export interface ChainExecutionDto {
  id: string;
  chainId: string;
  state: string;
  triggeredBy: string;
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
  jobExecutions: ChainJobExecutionDto[];
}

export interface ChainJobExecutionDto {
  id: string;
  jobId: string;
  jobName: string;
  state: string;
  startedAt?: string;
  completedAt?: string;
}

export interface CreateChainRequest {
  name: string;
  description?: string;
}

export interface UpdateChainRequest {
  name: string;
  description?: string;
}

export interface ChainMemberInput {
  jobId: string;
  executionOrder: number;
  dependsOnMemberIndex?: number;
  runOnUpstreamFailure?: boolean;
}

export interface ReplaceChainMembersRequest {
  members: ChainMemberInput[];
}

export interface TriggerChainRequest {
  triggeredBy: string;
}

// Job Dependencies
export interface JobDependencyDto {
  id: string;
  upstreamJobId: string;
  upstreamJobName: string;
  downstreamJobId: string;
  runOnFailure: boolean;
}

export interface AddJobDependencyRequest {
  upstreamJobId: string;
  runOnFailure?: boolean;
}

// Notifications
export interface NotificationRuleDto {
  id: string;
  name: string;
  description?: string;
  entityType: string;
  entityId?: string;
  eventTypes: string[];
  channel: string;
  channelConfig: Record<string, unknown>;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateNotificationRuleRequest {
  name: string;
  description?: string;
  entityType: string;
  entityId?: string;
  eventTypes: string[];
  channel: string;
  channelConfig: Record<string, unknown>;
  isEnabled?: boolean;
}

export interface UpdateNotificationRuleRequest {
  name: string;
  description?: string;
  entityType: string;
  entityId?: string;
  eventTypes: string[];
  channel: string;
  channelConfig: Record<string, unknown>;
  isEnabled: boolean;
}

export interface NotificationLogDto {
  id: string;
  notificationRuleId: string;
  ruleName?: string;
  eventType: string;
  entityType: string;
  entityId: string;
  channel: string;
  recipient: string;
  payload: Record<string, unknown>;
  success: boolean;
  errorMessage?: string;
  sentAt: string;
}
