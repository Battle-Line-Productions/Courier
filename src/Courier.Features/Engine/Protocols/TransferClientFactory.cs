using Courier.Domain.Entities;
using Courier.Domain.Protocols;
using FluentFTP;

namespace Courier.Features.Engine.Protocols;

public interface ITransferClientFactory
{
    ITransferClient Create(Connection connection, byte[]? decryptedPassword, byte[]? sshPrivateKey);
}

public class TransferClientFactory : ITransferClientFactory
{
    public ITransferClient Create(Connection connection, byte[]? decryptedPassword, byte[]? sshPrivateKey)
    {
        return connection.Protocol.ToLowerInvariant() switch
        {
            "sftp" => new SftpTransferClient(connection, decryptedPassword, sshPrivateKey),
            "ftp" => new FluentFtpTransferClient(connection, decryptedPassword, FtpEncryptionMode.None),
            "ftps" => new FluentFtpTransferClient(connection, decryptedPassword, DetermineEncryptionMode(connection)),
            _ => throw new ArgumentException($"Unsupported protocol: {connection.Protocol}")
        };
    }

    private static FtpEncryptionMode DetermineEncryptionMode(Connection connection)
        => connection.Port == 990 ? FtpEncryptionMode.Implicit : FtpEncryptionMode.Explicit;
}
