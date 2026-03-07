namespace Courier.Tests.JobEngine.Fixtures;

[CollectionDefinition("EngineTests")]
public class EngineTestCollection :
    ICollectionFixture<DatabaseFixture>,
    ICollectionFixture<SftpServerFixture>,
    ICollectionFixture<FtpServerFixture>
{
}
