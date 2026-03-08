using Courier.Features.Engine.Compression;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Compression;

public class CompressionRegistryTests
{
    private static ICompressionProvider CreateMockProvider(string formatKey)
    {
        var provider = Substitute.For<ICompressionProvider>();
        provider.FormatKey.Returns(formatKey);
        return provider;
    }

    [Fact]
    public void GetProvider_Zip_ReturnsZipProvider()
    {
        // Arrange
        var zipProvider = new ZipCompressionProvider();
        var registry = new CompressionProviderRegistry([zipProvider]);

        // Act
        var result = registry.GetProvider("zip");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<ZipCompressionProvider>();
        result.FormatKey.ShouldBe("zip");
    }

    [Fact]
    public void GetProvider_Tar_ReturnsTarProvider()
    {
        // Arrange
        var tarProvider = CreateMockProvider("tar");
        var registry = new CompressionProviderRegistry([tarProvider]);

        // Act
        var result = registry.GetProvider("tar");

        // Assert
        result.ShouldNotBeNull();
        result.FormatKey.ShouldBe("tar");
    }

    [Fact]
    public void GetProvider_Gzip_ReturnsGzipProvider()
    {
        // Arrange
        var gzipProvider = CreateMockProvider("gzip");
        var registry = new CompressionProviderRegistry([gzipProvider]);

        // Act
        var result = registry.GetProvider("gzip");

        // Assert
        result.ShouldNotBeNull();
        result.FormatKey.ShouldBe("gzip");
    }

    [Fact]
    public void GetProvider_TarGz_ReturnsTarGzProvider()
    {
        // Arrange
        var tarGzProvider = CreateMockProvider("tar.gz");
        var registry = new CompressionProviderRegistry([tarGzProvider]);

        // Act
        var result = registry.GetProvider("tar.gz");

        // Assert
        result.ShouldNotBeNull();
        result.FormatKey.ShouldBe("tar.gz");
    }

    [Fact]
    public void GetProvider_UnknownFormat_ThrowsInvalidOperationException()
    {
        // Arrange
        var zipProvider = new ZipCompressionProvider();
        var registry = new CompressionProviderRegistry([zipProvider]);

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => registry.GetProvider("rar"));
        ex.Message.ShouldContain("rar");
        ex.Message.ShouldContain("zip"); // Should list available formats
    }

    [Fact]
    public void GetProvider_CaseInsensitive_ResolvesCorrectly()
    {
        // Arrange
        var zipProvider = new ZipCompressionProvider();
        var registry = new CompressionProviderRegistry([zipProvider]);

        // Act
        var result = registry.GetProvider("ZIP");

        // Assert
        result.ShouldNotBeNull();
        result.FormatKey.ShouldBe("zip");
    }

    [Fact]
    public void GetProvider_MultipleProviders_ResolvesEachCorrectly()
    {
        // Arrange
        var zipProvider = CreateMockProvider("zip");
        var tarProvider = CreateMockProvider("tar");
        var gzipProvider = CreateMockProvider("gzip");
        var tarGzProvider = CreateMockProvider("tar.gz");
        var registry = new CompressionProviderRegistry([zipProvider, tarProvider, gzipProvider, tarGzProvider]);

        // Act & Assert
        registry.GetProvider("zip").FormatKey.ShouldBe("zip");
        registry.GetProvider("tar").FormatKey.ShouldBe("tar");
        registry.GetProvider("gzip").FormatKey.ShouldBe("gzip");
        registry.GetProvider("tar.gz").FormatKey.ShouldBe("tar.gz");
    }
}
