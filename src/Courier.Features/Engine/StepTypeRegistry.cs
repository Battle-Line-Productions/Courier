using Courier.Domain.Engine;

namespace Courier.Features.Engine;

public record StepTypeMetadata(string TypeKey, string DisplayName, string Category, string Description);

public class StepTypeRegistry
{
    private readonly Dictionary<string, IJobStep> _steps;

    private static readonly Dictionary<string, StepTypeMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase)
    {
        ["file.copy"] = new("file.copy", "Copy File", "file", "Copies files from source to destination"),
        ["file.move"] = new("file.move", "Move File", "file", "Moves files from source to destination"),
        ["file.delete"] = new("file.delete", "Delete File", "file", "Deletes specified files"),
        ["file.zip"] = new("file.zip", "Compress Files", "file", "Compresses files into an archive"),
        ["file.unzip"] = new("file.unzip", "Extract Archive", "file", "Extracts files from an archive"),
        ["sftp.upload"] = new("sftp.upload", "SFTP Upload", "transfer.sftp", "Uploads files via SFTP"),
        ["sftp.download"] = new("sftp.download", "SFTP Download", "transfer.sftp", "Downloads files via SFTP"),
        ["sftp.mkdir"] = new("sftp.mkdir", "SFTP Create Directory", "transfer.sftp", "Creates a remote directory via SFTP"),
        ["sftp.rmdir"] = new("sftp.rmdir", "SFTP Remove Directory", "transfer.sftp", "Removes a remote directory via SFTP"),
        ["sftp.list"] = new("sftp.list", "SFTP List Directory", "transfer.sftp", "Lists remote directory contents via SFTP"),
        ["ftp.upload"] = new("ftp.upload", "FTP Upload", "transfer.ftp", "Uploads files via FTP"),
        ["ftp.download"] = new("ftp.download", "FTP Download", "transfer.ftp", "Downloads files via FTP"),
        ["ftp.mkdir"] = new("ftp.mkdir", "FTP Create Directory", "transfer.ftp", "Creates a remote directory via FTP"),
        ["ftp.rmdir"] = new("ftp.rmdir", "FTP Remove Directory", "transfer.ftp", "Removes a remote directory via FTP"),
        ["ftp.list"] = new("ftp.list", "FTP List Directory", "transfer.ftp", "Lists remote directory contents via FTP"),
        ["ftps.upload"] = new("ftps.upload", "FTPS Upload", "transfer.ftps", "Uploads files via FTPS"),
        ["ftps.download"] = new("ftps.download", "FTPS Download", "transfer.ftps", "Downloads files via FTPS"),
        ["ftps.mkdir"] = new("ftps.mkdir", "FTPS Create Directory", "transfer.ftps", "Creates a remote directory via FTPS"),
        ["ftps.rmdir"] = new("ftps.rmdir", "FTPS Remove Directory", "transfer.ftps", "Removes a remote directory via FTPS"),
        ["ftps.list"] = new("ftps.list", "FTPS List Directory", "transfer.ftps", "Lists remote directory contents via FTPS"),
        ["pgp.encrypt"] = new("pgp.encrypt", "PGP Encrypt", "crypto", "Encrypts files using PGP"),
        ["pgp.decrypt"] = new("pgp.decrypt", "PGP Decrypt", "crypto", "Decrypts PGP-encrypted files"),
        ["pgp.sign"] = new("pgp.sign", "PGP Sign", "crypto", "Creates a PGP signature for files"),
        ["pgp.verify"] = new("pgp.verify", "PGP Verify", "crypto", "Verifies PGP signatures"),
        ["flow.if"] = new("flow.if", "If Condition", "flow", "Conditional branching based on expression evaluation"),
        ["flow.else"] = new("flow.else", "Else Branch", "flow", "Alternative branch for if condition"),
        ["flow.foreach"] = new("flow.foreach", "For Each Loop", "flow", "Iterates over a collection executing child steps"),
        ["flow.end"] = new("flow.end", "End Block", "flow", "Marks the end of a control flow block"),
        ["azure.function"] = new("azure.function", "Azure Function", "cloud", "Executes an Azure Function"),
    };

    public StepTypeRegistry(IEnumerable<IJobStep> steps)
    {
        _steps = steps.ToDictionary(s => s.TypeKey, StringComparer.OrdinalIgnoreCase);
    }

    public IJobStep Resolve(string typeKey)
        => _steps.TryGetValue(typeKey, out var step)
            ? step
            : throw new KeyNotFoundException($"No step handler registered for type key '{typeKey}'. Registered: [{string.Join(", ", _steps.Keys)}]");

    public IEnumerable<string> GetRegisteredTypes() => _steps.Keys;

    public IEnumerable<StepTypeMetadata> GetAllMetadata() => _metadata.Values;

    public StepTypeMetadata? GetMetadata(string typeKey)
        => _metadata.TryGetValue(typeKey, out var metadata) ? metadata : null;
}
