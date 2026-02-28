using System.Text.Json;
using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.Engine.Protocols;
using Courier.Infrastructure.Data;

namespace Courier.Features.Engine.Steps.Transfer;

public class FtpListStep : TransferStepBase
{
    public FtpListStep(CourierDbContext db, ICredentialEncryptor encryptor, JobConnectionRegistry registry)
        : base(db, encryptor, registry) { }

    public override string TypeKey => "ftp.list";
    protected override string ExpectedProtocol => "ftp";

    public override async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken ct)
    {
        var (client, error) = await ResolveClientAsync(config, ct);
        if (error is not null) return error;

        var remotePath = config.GetString("remote_path");
        var filePattern = config.GetStringOrDefault("file_pattern");
        var files = await client!.ListDirectoryAsync(remotePath, ct);

        if (!string.IsNullOrEmpty(filePattern) && filePattern != "*")
            files = files.Where(f => MatchesPattern(f.Name, filePattern)).ToList();

        var json = JsonSerializer.Serialize(files);
        return StepResult.Ok(outputs: new()
        {
            ["file_list"] = json,
            ["file_count"] = files.Count.ToString()
        });
    }

    public override Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(ValidateRequired(config, "connection_id", "remote_path"));

    private static bool MatchesPattern(string fileName, string pattern)
    {
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(fileName, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
