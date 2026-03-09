namespace Courier.Domain.Common;

public static class ErrorCodes
{
    // General (1000-1999)
    public const int ValidationFailed = 1000;
    public const int InvalidRequestFormat = 1001;
    public const int ResourceNotFound = 1030;
    public const int StateConflict = 1050;
    public const int DuplicateResource = 1052;
    public const int InternalServerError = 1099;

    // Job System (2000-2999)
    public const int JobNotEnabled = 2000;
    public const int JobHasNoSteps = 2001;
    public const int StepTypeNotRegistered = 2002;
    public const int InvalidStepOrder = 2003;
    public const int ExecutionNotFound = 2010;
    public const int ScheduleNotFound = 2020;
    public const int InvalidCronExpression = 2021;
    public const int ScheduleJobMismatch = 2023;

    // Job Versions (2030-2039)
    public const int JobVersionNotFound = 2030;

    // Execution Control (2040-2049)
    public const int ExecutionCannotBePaused = 2040;
    public const int ExecutionCannotBeResumed = 2041;
    public const int ExecutionCannotBeCancelled = 2042;

    // Connections (3000-3999)
    public const int ConnectionTestFailed = 3000;
    public const int AuthenticationFailed = 3001;
    public const int HostKeyMismatch = 3002;
    public const int HostUnreachable = 3003;
    public const int ConnectionInUse = 3010;
    public const int InvalidProtocolConfig = 3011;
    public const int KnownHostNotFound = 3020;
    public const int DuplicateKnownHostFingerprint = 3021;
    public const int KnownHostAlreadyApproved = 3022;

    // Keys (4000-4999)
    public const int KeyNotFound = 4000;
    public const int KeyGenerationFailed = 4001;
    public const int KeyImportFailed = 4002;
    public const int KeyImportInvalidFormat = 4003;
    public const int KeyAlreadyRetired = 4010;
    public const int KeyAlreadyRevoked = 4011;
    public const int KeyAlreadyActive = 4012;
    public const int KeyInUseByConnection = 4020;
    public const int InvalidKeyTransition = 4030;
    public const int KeySuccessorSelfReference = 4031;
    public const int KeySuccessorCircularChain = 4032;
    public const int KeySuccessorInvalidStatus = 4033;

    // Key Share Links (4040-4049)
    public const int ShareLinkNotFound = 4040;
    public const int ShareLinkExpired = 4041;
    public const int ShareLinkRevoked = 4042;
    public const int ShareLinksDisabled = 4043;
    public const int ShareLinkInvalidToken = 4044;

    // Transfer Operations (5000-5999)
    public const int TransferFailed = 5000;
    public const int ConnectionNotFound = 5001;
    public const int ProtocolMismatch = 5002;
    public const int RemotePathNotFound = 5003;
    public const int UploadFailed = 5010;
    public const int DownloadFailed = 5011;
    public const int ConnectionLost = 5020;
    public const int HostKeyVerificationFailed = 5021;
    public const int TlsCertificateRejected = 5022;
    public const int ResumeNotSupported = 5030;

    // Crypto Operations (6000-6999)
    public const int EncryptionFailed = 6000;
    public const int DecryptionFailed = 6001;
    public const int SigningFailed = 6002;
    public const int VerificationFailed = 6003;
    public const int KeyStatusInvalid = 6010;
    public const int KeyTypeInvalid = 6011;
    public const int WrongDecryptionKey = 6020;
    public const int CorruptedCiphertext = 6021;

    // Local Filesystem (8000-8099)
    public const int DirectoryNotFound = 8000;
    public const int FilesystemAccessDenied = 8001;

    // Azure Functions (9000-9099)
    public const int AzureFunctionTriggerFailed = 9000;
    public const int AzureFunctionExecutionFailed = 9001;
    public const int AzureFunctionPollTimeout = 9002;
    public const int AppInsightsQueryFailed = 9003;
    public const int EntraTokenAcquisitionFailed = 9004;
    public const int InvalidAzureFunctionConfig = 9005;

    // File Monitors (7000-7099)
    public const int MonitorNotActive = 7000;
    public const int MonitorNotInError = 7001;
    public const int MonitorAlreadyActive = 7002;
    public const int InvalidWatchTarget = 7003;
    public const int InvalidPollingInterval = 7004;

    // Chains & Dependencies (2050-2069)
    public const int ChainNotFound = 2050;
    public const int ChainNotEnabled = 2051;
    public const int ChainHasNoMembers = 2052;
    public const int CircularDependency = 2053;
    public const int SelfDependency = 2054;
    public const int DuplicateDependency = 2055;
    public const int ChainMemberJobNotFound = 2056;
    public const int ChainExecutionNotFound = 2057;
    public const int DependencyNotFound = 2058;
    public const int ChainScheduleNotFound = 2059;
    public const int ChainScheduleMismatch = 2060;

    // Tags (7100-7199)
    public const int DuplicateTagName = 7100;
    public const int InvalidTagEntityType = 7101;
    public const int TagEntityNotFound = 7102;

    // Notifications (7200-7299)
    public const int NotificationRuleNotFound = 7200;
    public const int DuplicateNotificationRuleName = 7201;
    public const int InvalidNotificationChannel = 7202;
    public const int InvalidNotificationEntityType = 7203;
    public const int NotificationDispatchFailed = 7204;
    public const int InvalidChannelConfig = 7205;
    public const int NotificationTestFailed = 7206;

    // Authentication & Authorization (10000-10099)
    public const int InvalidCredentials = 10000;
    public const int AccountLocked = 10001;
    public const int AccountDisabled = 10002;
    public const int InvalidRefreshToken = 10003;
    public const int RefreshTokenExpired = 10004;
    public const int SetupNotCompleted = 10005;
    public const int SetupAlreadyCompleted = 10006;
    public const int Unauthorized = 10007;
    public const int Forbidden = 10008;
    public const int DuplicateUsername = 10009;
    public const int CannotDeleteSelf = 10010;
    public const int CannotDemoteLastAdmin = 10011;
    public const int WeakPassword = 10012;
    public const int InvalidCurrentPassword = 10013;
    public const int UserNotFound = 10014;

    // Feedback / GitHub (11000-11099)
    public const int GitHubApiUnavailable = 11000;
    public const int GitHubAuthFailed = 11001;
    public const int GitHubRateLimited = 11002;
    public const int GitHubIssueNotFound = 11003;
    public const int GitHubValidationFailed = 11004;
    public const int GitHubNotConfigured = 11005;
    public const int GitHubAccountNotLinked = 11006;
    public const int GitHubOAuthFailed = 11007;
}

public static class ErrorMessages
{
    private static readonly Dictionary<int, string> SystemMessages = new()
    {
        [ErrorCodes.ValidationFailed] = "Validation failed",
        [ErrorCodes.InvalidRequestFormat] = "Invalid request format",
        [ErrorCodes.ResourceNotFound] = "Resource not found",
        [ErrorCodes.StateConflict] = "State conflict",
        [ErrorCodes.DuplicateResource] = "Duplicate resource",
        [ErrorCodes.InternalServerError] = "Internal server error",
        [ErrorCodes.JobNotEnabled] = "Job not enabled",
        [ErrorCodes.JobHasNoSteps] = "Job has no steps",
        [ErrorCodes.StepTypeNotRegistered] = "Step type not registered",
        [ErrorCodes.InvalidStepOrder] = "Invalid step order",
        [ErrorCodes.ExecutionNotFound] = "Execution not found",
        [ErrorCodes.ScheduleNotFound] = "Schedule not found",
        [ErrorCodes.InvalidCronExpression] = "Invalid cron expression",
        [ErrorCodes.ScheduleJobMismatch] = "Schedule does not belong to this job",
        [ErrorCodes.JobVersionNotFound] = "Job version not found",
        [ErrorCodes.ExecutionCannotBePaused] = "Execution cannot be paused",
        [ErrorCodes.ExecutionCannotBeResumed] = "Execution cannot be resumed",
        [ErrorCodes.ExecutionCannotBeCancelled] = "Execution cannot be cancelled",
        [ErrorCodes.ConnectionTestFailed] = "Connection test failed",
        [ErrorCodes.AuthenticationFailed] = "Authentication failed",
        [ErrorCodes.HostKeyMismatch] = "Host key mismatch",
        [ErrorCodes.HostUnreachable] = "Host unreachable",
        [ErrorCodes.ConnectionInUse] = "Connection in use",
        [ErrorCodes.InvalidProtocolConfig] = "Invalid protocol configuration",
        [ErrorCodes.KnownHostNotFound] = "Known host not found",
        [ErrorCodes.DuplicateKnownHostFingerprint] = "Duplicate known host fingerprint",
        [ErrorCodes.KnownHostAlreadyApproved] = "Known host already approved",
        [ErrorCodes.KeyNotFound] = "Key not found",
        [ErrorCodes.KeyGenerationFailed] = "Key generation failed",
        [ErrorCodes.KeyImportFailed] = "Key import failed",
        [ErrorCodes.KeyImportInvalidFormat] = "Key import invalid format",
        [ErrorCodes.KeyAlreadyRetired] = "Key already retired",
        [ErrorCodes.KeyAlreadyRevoked] = "Key already revoked",
        [ErrorCodes.KeyAlreadyActive] = "Key already active",
        [ErrorCodes.KeyInUseByConnection] = "Key in use by connection",
        [ErrorCodes.InvalidKeyTransition] = "Invalid key status transition",
        [ErrorCodes.KeySuccessorSelfReference] = "Cannot set key as its own successor",
        [ErrorCodes.KeySuccessorCircularChain] = "Circular successor chain detected",
        [ErrorCodes.KeySuccessorInvalidStatus] = "Successor key has invalid status",
        [ErrorCodes.ShareLinkNotFound] = "Share link not found",
        [ErrorCodes.ShareLinkExpired] = "Share link expired",
        [ErrorCodes.ShareLinkRevoked] = "Share link revoked",
        [ErrorCodes.ShareLinksDisabled] = "Public key share links are disabled",
        [ErrorCodes.ShareLinkInvalidToken] = "Invalid share link token",
        [ErrorCodes.TransferFailed] = "Transfer failed",
        [ErrorCodes.ConnectionNotFound] = "Connection not found",
        [ErrorCodes.ProtocolMismatch] = "Protocol mismatch",
        [ErrorCodes.RemotePathNotFound] = "Remote path not found",
        [ErrorCodes.UploadFailed] = "Upload failed",
        [ErrorCodes.DownloadFailed] = "Download failed",
        [ErrorCodes.ConnectionLost] = "Connection lost",
        [ErrorCodes.HostKeyVerificationFailed] = "Host key verification failed",
        [ErrorCodes.TlsCertificateRejected] = "TLS certificate rejected",
        [ErrorCodes.ResumeNotSupported] = "Resume not supported",
        [ErrorCodes.EncryptionFailed] = "Encryption failed",
        [ErrorCodes.DecryptionFailed] = "Decryption failed",
        [ErrorCodes.SigningFailed] = "Signing failed",
        [ErrorCodes.VerificationFailed] = "Verification failed",
        [ErrorCodes.KeyStatusInvalid] = "Key status invalid",
        [ErrorCodes.KeyTypeInvalid] = "Key type invalid",
        [ErrorCodes.WrongDecryptionKey] = "Wrong decryption key",
        [ErrorCodes.CorruptedCiphertext] = "Corrupted ciphertext",
        [ErrorCodes.DirectoryNotFound] = "Directory not found",
        [ErrorCodes.FilesystemAccessDenied] = "Filesystem access denied",
        [ErrorCodes.AzureFunctionTriggerFailed] = "Azure Function trigger failed",
        [ErrorCodes.AzureFunctionExecutionFailed] = "Azure Function execution failed",
        [ErrorCodes.AzureFunctionPollTimeout] = "Azure Function poll timeout",
        [ErrorCodes.AppInsightsQueryFailed] = "Application Insights query failed",
        [ErrorCodes.EntraTokenAcquisitionFailed] = "Entra token acquisition failed",
        [ErrorCodes.InvalidAzureFunctionConfig] = "Invalid Azure Function configuration",
        [ErrorCodes.MonitorNotActive] = "Monitor not active",
        [ErrorCodes.MonitorNotInError] = "Monitor not in error state",
        [ErrorCodes.MonitorAlreadyActive] = "Monitor already active",
        [ErrorCodes.InvalidWatchTarget] = "Invalid watch target",
        [ErrorCodes.InvalidPollingInterval] = "Invalid polling interval",
        [ErrorCodes.ChainNotFound] = "Chain not found",
        [ErrorCodes.ChainNotEnabled] = "Chain not enabled",
        [ErrorCodes.ChainHasNoMembers] = "Chain has no members",
        [ErrorCodes.CircularDependency] = "Circular dependency detected",
        [ErrorCodes.SelfDependency] = "Self-dependency not allowed",
        [ErrorCodes.DuplicateDependency] = "Duplicate dependency",
        [ErrorCodes.ChainMemberJobNotFound] = "Chain member job not found",
        [ErrorCodes.ChainExecutionNotFound] = "Chain execution not found",
        [ErrorCodes.DependencyNotFound] = "Dependency not found",
        [ErrorCodes.ChainScheduleNotFound] = "Chain schedule not found",
        [ErrorCodes.ChainScheduleMismatch] = "Schedule does not belong to this chain",
        [ErrorCodes.DuplicateTagName] = "Duplicate tag name",
        [ErrorCodes.InvalidTagEntityType] = "Invalid entity type for tagging",
        [ErrorCodes.TagEntityNotFound] = "Tagged entity not found",
        [ErrorCodes.InvalidCredentials] = "Invalid credentials",
        [ErrorCodes.AccountLocked] = "Account locked",
        [ErrorCodes.AccountDisabled] = "Account disabled",
        [ErrorCodes.InvalidRefreshToken] = "Invalid refresh token",
        [ErrorCodes.RefreshTokenExpired] = "Refresh token expired",
        [ErrorCodes.SetupNotCompleted] = "Setup not completed",
        [ErrorCodes.SetupAlreadyCompleted] = "Setup already completed",
        [ErrorCodes.Unauthorized] = "Unauthorized",
        [ErrorCodes.Forbidden] = "Forbidden",
        [ErrorCodes.DuplicateUsername] = "Duplicate username",
        [ErrorCodes.CannotDeleteSelf] = "Cannot delete own account",
        [ErrorCodes.CannotDemoteLastAdmin] = "Cannot demote the last admin",
        [ErrorCodes.WeakPassword] = "Password does not meet requirements",
        [ErrorCodes.InvalidCurrentPassword] = "Invalid current password",
        [ErrorCodes.UserNotFound] = "User not found",

        // Feedback / GitHub
        [ErrorCodes.GitHubApiUnavailable] = "GitHub API unavailable",
        [ErrorCodes.GitHubAuthFailed] = "GitHub authentication failed",
        [ErrorCodes.GitHubRateLimited] = "GitHub rate limit exceeded",
        [ErrorCodes.GitHubIssueNotFound] = "GitHub issue not found",
        [ErrorCodes.GitHubValidationFailed] = "GitHub validation failed",
        [ErrorCodes.GitHubNotConfigured] = "GitHub integration not configured",
        [ErrorCodes.GitHubAccountNotLinked] = "GitHub account not linked",
        [ErrorCodes.GitHubOAuthFailed] = "GitHub OAuth failed",
    };

    public static ApiError Create(int code, string message, IReadOnlyList<FieldError>? details = null)
    {
        var systemMessage = SystemMessages.GetValueOrDefault(code, "Unknown error");
        return new ApiError(code, systemMessage, message, details);
    }
}
