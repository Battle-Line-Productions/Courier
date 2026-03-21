namespace Courier.Domain.Enums;

public enum AuditableEntityType
{
    Job,
    JobExecution,
    StepExecution,
    Connection,
    PgpKey,
    SshKey,
    FileMonitor,
    Tag,
    Chain,
    ChainExecution,
    NotificationRule,
    User,
    KnownHost,
    AuthProvider
}
