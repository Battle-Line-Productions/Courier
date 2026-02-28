using Courier.Domain.Encryption;
using Courier.Features.Engine.Protocols;
using Courier.Features.Engine.Steps.Transfer;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Steps.Transfer;

public class TransferStepTypeKeyTests
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly JobConnectionRegistry _registry;

    public TransferStepTypeKeyTests()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new CourierDbContext(options);
        _encryptor = Substitute.For<ICredentialEncryptor>();
        _registry = new JobConnectionRegistry(Substitute.For<ITransferClientFactory>());
    }

    [Theory]
    [InlineData(typeof(SftpUploadStep), "sftp.upload")]
    [InlineData(typeof(SftpDownloadStep), "sftp.download")]
    [InlineData(typeof(SftpMkdirStep), "sftp.mkdir")]
    [InlineData(typeof(SftpRmdirStep), "sftp.rmdir")]
    [InlineData(typeof(SftpListStep), "sftp.list")]
    [InlineData(typeof(FtpUploadStep), "ftp.upload")]
    [InlineData(typeof(FtpDownloadStep), "ftp.download")]
    [InlineData(typeof(FtpMkdirStep), "ftp.mkdir")]
    [InlineData(typeof(FtpRmdirStep), "ftp.rmdir")]
    [InlineData(typeof(FtpListStep), "ftp.list")]
    [InlineData(typeof(FtpsUploadStep), "ftps.upload")]
    [InlineData(typeof(FtpsDownloadStep), "ftps.download")]
    [InlineData(typeof(FtpsMkdirStep), "ftps.mkdir")]
    [InlineData(typeof(FtpsRmdirStep), "ftps.rmdir")]
    [InlineData(typeof(FtpsListStep), "ftps.list")]
    public void TypeKey_IsCorrect(Type stepType, string expectedKey)
    {
        var step = (TransferStepBase)Activator.CreateInstance(stepType, _db, _encryptor, _registry)!;
        step.TypeKey.ShouldBe(expectedKey);
    }
}
