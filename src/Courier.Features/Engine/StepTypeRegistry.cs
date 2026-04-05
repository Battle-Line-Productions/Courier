using Courier.Domain.Engine;

namespace Courier.Features.Engine;

public record StepOutputMeta(string Key, string Description, string ValueType, bool Conditional = false);

public record StepInputMeta(string Key, string Description, bool Required, bool SupportsContextRef = false);

public record StepTypeMetadata(
    string TypeKey,
    string DisplayName,
    string Category,
    string Description,
    IReadOnlyList<StepOutputMeta>? Outputs = null,
    IReadOnlyList<StepInputMeta>? Inputs = null);

public class StepTypeRegistry
{
    private readonly Dictionary<string, IJobStep> _steps;

    private static readonly Dictionary<string, StepTypeMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase)
    {
        ["file.copy"] = new("file.copy", "Copy File", "file", "Copies files from source to destination",
            Outputs: [new("copied_file", "Destination file path", "string")],
            Inputs: [
                new("sourcePath", "Source file or directory path", Required: true, SupportsContextRef: true),
                new("destinationPath", "Destination file path", Required: true, SupportsContextRef: true),
                new("overwrite", "Overwrite existing files", Required: false),
            ]),
        ["file.move"] = new("file.move", "Move File", "file", "Moves files from source to destination",
            Outputs: [new("moved_file", "Destination file path", "string")],
            Inputs: [
                new("sourcePath", "Source file or directory path", Required: true, SupportsContextRef: true),
                new("destinationPath", "Destination file path", Required: true, SupportsContextRef: true),
                new("overwrite", "Overwrite existing files", Required: false),
            ]),
        ["file.delete"] = new("file.delete", "Delete File", "file", "Deletes specified files",
            Outputs: [
                new("deleted_file", "Path of the deleted file", "string"),
                new("existed", "Whether the file existed before deletion", "boolean"),
            ],
            Inputs: [
                new("path", "File path to delete", Required: true, SupportsContextRef: true),
                new("failIfNotFound", "Fail if file does not exist", Required: false),
            ]),
        ["file.zip"] = new("file.zip", "Compress Files", "file", "Compresses files into an archive",
            Outputs: [
                new("archive_path", "Path to the created ZIP archive", "string"),
                new("split_parts", "List of split archive parts", "string[]", Conditional: true),
                new("split_count", "Number of split parts", "number", Conditional: true),
            ],
            Inputs: [
                new("sourcePath", "Source file or directory to compress", Required: true, SupportsContextRef: true),
                new("outputPath", "Output archive path", Required: true, SupportsContextRef: true),
                new("password", "Optional archive password", Required: false),
            ]),
        ["file.unzip"] = new("file.unzip", "Extract Archive", "file", "Extracts files from an archive",
            Outputs: [
                new("extracted_directory", "Output directory path", "string"),
                new("extracted_files", "List of extracted file paths", "string[]", Conditional: true),
            ],
            Inputs: [
                new("archivePath", "Path to the archive file", Required: true, SupportsContextRef: true),
                new("outputDirectory", "Directory to extract into", Required: true, SupportsContextRef: true),
                new("password", "Optional archive password", Required: false),
            ]),
        ["sftp.upload"] = new("sftp.upload", "SFTP Upload", "transfer.sftp", "Uploads files via SFTP",
            Outputs: [new("uploaded_file", "Remote file path after upload", "string")],
            Inputs: [
                new("connectionId", "SFTP connection to use", Required: true),
                new("localPath", "Local file path to upload", Required: true, SupportsContextRef: true),
                new("remotePath", "Remote destination path", Required: true, SupportsContextRef: true),
            ]),
        ["sftp.download"] = new("sftp.download", "SFTP Download", "transfer.sftp", "Downloads files via SFTP",
            Outputs: [new("downloaded_file", "Local file path after download", "string")],
            Inputs: [
                new("connectionId", "SFTP connection to use", Required: true),
                new("remotePath", "Remote file path to download", Required: true, SupportsContextRef: true),
                new("localPath", "Local destination path", Required: true, SupportsContextRef: true),
            ]),
        ["sftp.mkdir"] = new("sftp.mkdir", "SFTP Create Directory", "transfer.sftp", "Creates a remote directory via SFTP",
            Outputs: [new("created_directory", "Created directory path", "string")],
            Inputs: [
                new("connectionId", "SFTP connection to use", Required: true),
                new("remotePath", "Remote directory path to create", Required: true, SupportsContextRef: true),
            ]),
        ["sftp.rmdir"] = new("sftp.rmdir", "SFTP Remove Directory", "transfer.sftp", "Removes a remote directory via SFTP",
            Inputs: [
                new("connectionId", "SFTP connection to use", Required: true),
                new("remotePath", "Remote directory path to remove", Required: true, SupportsContextRef: true),
                new("recursive", "Remove contents recursively", Required: false),
            ]),
        ["sftp.list"] = new("sftp.list", "SFTP List Directory", "transfer.sftp", "Lists remote directory contents via SFTP",
            Outputs: [
                new("file_list", "JSON array of remote files", "json"),
                new("file_count", "Number of files found", "number"),
            ],
            Inputs: [
                new("connectionId", "SFTP connection to use", Required: true),
                new("remotePath", "Remote directory path to list", Required: true, SupportsContextRef: true),
                new("filePattern", "Optional file name filter pattern", Required: false),
            ]),
        ["ftp.upload"] = new("ftp.upload", "FTP Upload", "transfer.ftp", "Uploads files via FTP",
            Outputs: [new("uploaded_file", "Remote file path after upload", "string")],
            Inputs: [
                new("connectionId", "FTP connection to use", Required: true),
                new("localPath", "Local file path to upload", Required: true, SupportsContextRef: true),
                new("remotePath", "Remote destination path", Required: true, SupportsContextRef: true),
            ]),
        ["ftp.download"] = new("ftp.download", "FTP Download", "transfer.ftp", "Downloads files via FTP",
            Outputs: [new("downloaded_file", "Local file path after download", "string")],
            Inputs: [
                new("connectionId", "FTP connection to use", Required: true),
                new("remotePath", "Remote file path to download", Required: true, SupportsContextRef: true),
                new("localPath", "Local destination path", Required: true, SupportsContextRef: true),
            ]),
        ["ftp.mkdir"] = new("ftp.mkdir", "FTP Create Directory", "transfer.ftp", "Creates a remote directory via FTP",
            Outputs: [new("created_directory", "Created directory path", "string")],
            Inputs: [
                new("connectionId", "FTP connection to use", Required: true),
                new("remotePath", "Remote directory path to create", Required: true, SupportsContextRef: true),
            ]),
        ["ftp.rmdir"] = new("ftp.rmdir", "FTP Remove Directory", "transfer.ftp", "Removes a remote directory via FTP",
            Inputs: [
                new("connectionId", "FTP connection to use", Required: true),
                new("remotePath", "Remote directory path to remove", Required: true, SupportsContextRef: true),
                new("recursive", "Remove contents recursively", Required: false),
            ]),
        ["ftp.list"] = new("ftp.list", "FTP List Directory", "transfer.ftp", "Lists remote directory contents via FTP",
            Outputs: [
                new("file_list", "JSON array of remote files", "json"),
                new("file_count", "Number of files found", "number"),
            ],
            Inputs: [
                new("connectionId", "FTP connection to use", Required: true),
                new("remotePath", "Remote directory path to list", Required: true, SupportsContextRef: true),
                new("filePattern", "Optional file name filter pattern", Required: false),
            ]),
        ["ftps.upload"] = new("ftps.upload", "FTPS Upload", "transfer.ftps", "Uploads files via FTPS",
            Outputs: [new("uploaded_file", "Remote file path after upload", "string")],
            Inputs: [
                new("connectionId", "FTPS connection to use", Required: true),
                new("localPath", "Local file path to upload", Required: true, SupportsContextRef: true),
                new("remotePath", "Remote destination path", Required: true, SupportsContextRef: true),
            ]),
        ["ftps.download"] = new("ftps.download", "FTPS Download", "transfer.ftps", "Downloads files via FTPS",
            Outputs: [new("downloaded_file", "Local file path after download", "string")],
            Inputs: [
                new("connectionId", "FTPS connection to use", Required: true),
                new("remotePath", "Remote file path to download", Required: true, SupportsContextRef: true),
                new("localPath", "Local destination path", Required: true, SupportsContextRef: true),
            ]),
        ["ftps.mkdir"] = new("ftps.mkdir", "FTPS Create Directory", "transfer.ftps", "Creates a remote directory via FTPS",
            Outputs: [new("created_directory", "Created directory path", "string")],
            Inputs: [
                new("connectionId", "FTPS connection to use", Required: true),
                new("remotePath", "Remote directory path to create", Required: true, SupportsContextRef: true),
            ]),
        ["ftps.rmdir"] = new("ftps.rmdir", "FTPS Remove Directory", "transfer.ftps", "Removes a remote directory via FTPS",
            Inputs: [
                new("connectionId", "FTPS connection to use", Required: true),
                new("remotePath", "Remote directory path to remove", Required: true, SupportsContextRef: true),
                new("recursive", "Remove contents recursively", Required: false),
            ]),
        ["ftps.list"] = new("ftps.list", "FTPS List Directory", "transfer.ftps", "Lists remote directory contents via FTPS",
            Outputs: [
                new("file_list", "JSON array of remote files", "json"),
                new("file_count", "Number of files found", "number"),
            ],
            Inputs: [
                new("connectionId", "FTPS connection to use", Required: true),
                new("remotePath", "Remote directory path to list", Required: true, SupportsContextRef: true),
                new("filePattern", "Optional file name filter pattern", Required: false),
            ]),
        ["pgp.encrypt"] = new("pgp.encrypt", "PGP Encrypt", "crypto", "Encrypts files using PGP",
            Outputs: [new("encrypted_file", "Encrypted file path", "string")],
            Inputs: [
                new("inputPath", "Input file path to encrypt", Required: true, SupportsContextRef: true),
                new("outputPath", "Output encrypted file path", Required: true, SupportsContextRef: true),
                new("recipientKeyIds", "PGP key IDs for encryption", Required: true),
            ]),
        ["pgp.decrypt"] = new("pgp.decrypt", "PGP Decrypt", "crypto", "Decrypts PGP-encrypted files",
            Outputs: [new("decrypted_file", "Decrypted file path", "string")],
            Inputs: [
                new("inputPath", "Input file path to decrypt", Required: true, SupportsContextRef: true),
                new("outputPath", "Output decrypted file path", Required: true, SupportsContextRef: true),
                new("privateKeyId", "PGP private key ID for decryption", Required: true),
            ]),
        ["pgp.sign"] = new("pgp.sign", "PGP Sign", "crypto", "Creates a PGP signature for files",
            Outputs: [new("signature_file", "Signature file path", "string")],
            Inputs: [
                new("inputPath", "Input file path to sign", Required: true, SupportsContextRef: true),
                new("outputPath", "Output signature file path", Required: true, SupportsContextRef: true),
                new("signingKeyId", "PGP key ID for signing", Required: true),
            ]),
        ["pgp.verify"] = new("pgp.verify", "PGP Verify", "crypto", "Verifies PGP signatures",
            Outputs: [
                new("verify_status", "Verification result status", "string"),
                new("is_valid", "Whether signature is valid", "boolean"),
            ],
            Inputs: [
                new("inputPath", "Input file path to verify", Required: true, SupportsContextRef: true),
                new("signaturePath", "Detached signature file path", Required: false, SupportsContextRef: true),
                new("signerKeyIds", "Expected signer key IDs", Required: false),
            ]),
        ["flow.if"] = new("flow.if", "If Condition", "flow", "Conditional branching based on expression evaluation",
            Inputs: [
                new("left", "Left operand value or context reference", Required: true, SupportsContextRef: true),
                new("operator", "Comparison operator", Required: true),
                new("right", "Right operand value or context reference", Required: false, SupportsContextRef: true),
            ]),
        ["flow.else"] = new("flow.else", "Else Branch", "flow", "Alternative branch for if condition"),
        ["flow.foreach"] = new("flow.foreach", "For Each Loop", "flow", "Iterates over a collection executing child steps",
            Inputs: [
                new("source", "Context reference to array to iterate over", Required: true, SupportsContextRef: true),
            ]),
        ["flow.end"] = new("flow.end", "End Block", "flow", "Marks the end of a control flow block"),
        ["azure_function.execute"] = new("azure_function.execute", "Azure Function", "cloud", "Executes an Azure Function via HTTP trigger",
            Outputs: [
                new("function_success", "Whether the function succeeded", "boolean"),
                new("callback_result", "Output payload from the Azure Function callback", "object"),
                new("http_status", "HTTP status code from the function trigger", "number"),
            ],
            Inputs: [
                new("connection_id", "Azure Function connection to use", Required: true),
                new("function_name", "Azure Function name to invoke", Required: true),
                new("input_payload", "JSON payload to send to the function", Required: false, SupportsContextRef: true),
                new("wait_for_callback", "Whether to wait for the function to call back (default: true)", Required: false),
                new("max_wait_sec", "Maximum seconds to wait for callback (default: 3600)", Required: false),
                new("poll_interval_sec", "Seconds between DB polls for callback (default: 5)", Required: false),
            ]),
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
