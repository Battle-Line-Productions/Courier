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

    // Connections (3000-3999)
    public const int ConnectionTestFailed = 3000;
    public const int AuthenticationFailed = 3001;
    public const int HostKeyMismatch = 3002;
    public const int HostUnreachable = 3003;
    public const int ConnectionInUse = 3010;
    public const int InvalidProtocolConfig = 3011;

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
        [ErrorCodes.ConnectionTestFailed] = "Connection test failed",
        [ErrorCodes.AuthenticationFailed] = "Authentication failed",
        [ErrorCodes.HostKeyMismatch] = "Host key mismatch",
        [ErrorCodes.HostUnreachable] = "Host unreachable",
        [ErrorCodes.ConnectionInUse] = "Connection in use",
        [ErrorCodes.InvalidProtocolConfig] = "Invalid protocol configuration",
        [ErrorCodes.KeyNotFound] = "Key not found",
        [ErrorCodes.KeyGenerationFailed] = "Key generation failed",
        [ErrorCodes.KeyImportFailed] = "Key import failed",
        [ErrorCodes.KeyImportInvalidFormat] = "Key import invalid format",
        [ErrorCodes.KeyAlreadyRetired] = "Key already retired",
        [ErrorCodes.KeyAlreadyRevoked] = "Key already revoked",
        [ErrorCodes.KeyAlreadyActive] = "Key already active",
        [ErrorCodes.KeyInUseByConnection] = "Key in use by connection",
        [ErrorCodes.InvalidKeyTransition] = "Invalid key status transition",
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
    };

    public static ApiError Create(int code, string message, IReadOnlyList<FieldError>? details = null)
    {
        var systemMessage = SystemMessages.GetValueOrDefault(code, "Unknown error");
        return new ApiError(code, systemMessage, message, details);
    }
}
