using FluentValidation;

namespace Courier.Features.Connections;

public class CreateConnectionValidator : AbstractValidator<CreateConnectionRequest>
{
    private static readonly string[] ValidProtocols = ["sftp", "ftp", "ftps", "azure_function"];
    private static readonly string[] ValidAuthMethods = ["password", "ssh_key", "password_and_ssh_key", "service_principal", "function_key"];
    private static readonly string[] ValidHostKeyPolicies = ["trust_on_first_use", "always_trust", "manual"];
    private static readonly string[] ValidTlsCertPolicies = ["system_trust", "pinned_thumbprint", "insecure"];

    public CreateConnectionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Connection name is required.")
            .MaximumLength(200).WithMessage("Connection name must not exceed 200 characters.");

        RuleFor(x => x.Protocol)
            .NotEmpty().WithMessage("Protocol is required.")
            .Must(v => ValidProtocols.Contains(v))
            .WithMessage("Protocol must be one of: sftp, ftp, ftps, azure_function.");

        RuleFor(x => x.Host)
            .NotEmpty().WithMessage("Host is required.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Port must be between 1 and 65535.")
            .When(x => x.Port.HasValue);

        RuleFor(x => x.AuthMethod)
            .NotEmpty().WithMessage("Auth method is required.")
            .Must(v => ValidAuthMethods.Contains(v))
            .WithMessage("Auth method must be one of: password, ssh_key, password_and_ssh_key, service_principal, function_key.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required when auth method includes password.")
            .When(x => x.AuthMethod is "password" or "password_and_ssh_key");

        RuleFor(x => x.SshKeyId)
            .NotNull().WithMessage("SSH key ID is required when auth method includes ssh_key.")
            .When(x => x.AuthMethod is "ssh_key" or "password_and_ssh_key");

        // Azure Function-specific validation
        RuleFor(x => x.AuthMethod)
            .Equal("function_key").WithMessage("Azure Function connections require function_key auth method.")
            .When(x => x.Protocol == "azure_function");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Function key is required for Azure Function connections.")
            .When(x => x.Protocol == "azure_function");

        RuleFor(x => x.HostKeyPolicy)
            .Must(v => ValidHostKeyPolicies.Contains(v))
            .WithMessage("Host key policy must be one of: trust_on_first_use, always_trust, manual.")
            .When(x => x.HostKeyPolicy is not null);

        RuleFor(x => x.TlsCertPolicy)
            .Must(v => ValidTlsCertPolicies.Contains(v))
            .WithMessage("TLS cert policy must be one of: system_trust, pinned_thumbprint, insecure.")
            .When(x => x.TlsCertPolicy is not null);

        RuleFor(x => x.TlsPinnedThumbprint)
            .NotEmpty().WithMessage("TLS pinned thumbprint is required when TLS cert policy is pinned_thumbprint.")
            .When(x => x.TlsCertPolicy == "pinned_thumbprint");

        RuleFor(x => x.TransportRetries)
            .InclusiveBetween(0, 3).WithMessage("Transport retries must be between 0 and 3.")
            .When(x => x.TransportRetries.HasValue);

        RuleFor(x => x.ConnectTimeoutSec)
            .GreaterThan(0).WithMessage("Connect timeout must be greater than 0.")
            .When(x => x.ConnectTimeoutSec.HasValue);

        RuleFor(x => x.OperationTimeoutSec)
            .GreaterThan(0).WithMessage("Operation timeout must be greater than 0.")
            .When(x => x.OperationTimeoutSec.HasValue);

        RuleFor(x => x.KeepaliveIntervalSec)
            .GreaterThan(0).WithMessage("Keepalive interval must be greater than 0.")
            .When(x => x.KeepaliveIntervalSec.HasValue);
    }
}

public class UpdateConnectionValidator : AbstractValidator<UpdateConnectionRequest>
{
    private static readonly string[] ValidProtocols = ["sftp", "ftp", "ftps", "azure_function"];
    private static readonly string[] ValidAuthMethods = ["password", "ssh_key", "password_and_ssh_key", "service_principal", "function_key"];
    private static readonly string[] ValidHostKeyPolicies = ["trust_on_first_use", "always_trust", "manual"];
    private static readonly string[] ValidTlsCertPolicies = ["system_trust", "pinned_thumbprint", "insecure"];
    private static readonly string[] ValidStatuses = ["active", "disabled"];

    public UpdateConnectionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Connection name is required.")
            .MaximumLength(200).WithMessage("Connection name must not exceed 200 characters.");

        RuleFor(x => x.Protocol)
            .NotEmpty().WithMessage("Protocol is required.")
            .Must(v => ValidProtocols.Contains(v))
            .WithMessage("Protocol must be one of: sftp, ftp, ftps, azure_function.");

        RuleFor(x => x.Host)
            .NotEmpty().WithMessage("Host is required.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535).WithMessage("Port must be between 1 and 65535.")
            .When(x => x.Port.HasValue);

        RuleFor(x => x.AuthMethod)
            .NotEmpty().WithMessage("Auth method is required.")
            .Must(v => ValidAuthMethods.Contains(v))
            .WithMessage("Auth method must be one of: password, ssh_key, password_and_ssh_key, service_principal, function_key.");

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.");

        RuleFor(x => x.SshKeyId)
            .NotNull().WithMessage("SSH key ID is required when auth method includes ssh_key.")
            .When(x => x.AuthMethod is "ssh_key" or "password_and_ssh_key");

        // Azure Function-specific validation
        RuleFor(x => x.AuthMethod)
            .Equal("function_key").WithMessage("Azure Function connections require function_key auth method.")
            .When(x => x.Protocol == "azure_function");

        RuleFor(x => x.HostKeyPolicy)
            .Must(v => ValidHostKeyPolicies.Contains(v))
            .WithMessage("Host key policy must be one of: trust_on_first_use, always_trust, manual.")
            .When(x => x.HostKeyPolicy is not null);

        RuleFor(x => x.TlsCertPolicy)
            .Must(v => ValidTlsCertPolicies.Contains(v))
            .WithMessage("TLS cert policy must be one of: system_trust, pinned_thumbprint, insecure.")
            .When(x => x.TlsCertPolicy is not null);

        RuleFor(x => x.TlsPinnedThumbprint)
            .NotEmpty().WithMessage("TLS pinned thumbprint is required when TLS cert policy is pinned_thumbprint.")
            .When(x => x.TlsCertPolicy == "pinned_thumbprint");

        RuleFor(x => x.TransportRetries)
            .InclusiveBetween(0, 3).WithMessage("Transport retries must be between 0 and 3.")
            .When(x => x.TransportRetries.HasValue);

        RuleFor(x => x.ConnectTimeoutSec)
            .GreaterThan(0).WithMessage("Connect timeout must be greater than 0.")
            .When(x => x.ConnectTimeoutSec.HasValue);

        RuleFor(x => x.OperationTimeoutSec)
            .GreaterThan(0).WithMessage("Operation timeout must be greater than 0.")
            .When(x => x.OperationTimeoutSec.HasValue);

        RuleFor(x => x.KeepaliveIntervalSec)
            .GreaterThan(0).WithMessage("Keepalive interval must be greater than 0.")
            .When(x => x.KeepaliveIntervalSec.HasValue);

        RuleFor(x => x.Status)
            .Must(v => ValidStatuses.Contains(v))
            .WithMessage("Status must be one of: active, disabled.")
            .When(x => x.Status is not null);
    }
}
