namespace Courier.Features.Engine.Compression;

public interface ICompressionProvider
{
    string FormatKey { get; }

    Task<CompressionResult> CompressAsync(
        CompressRequest request,
        IProgress<CompressionProgress>? progress,
        CancellationToken ct);

    Task<CompressionResult> DecompressAsync(
        DecompressRequest request,
        IProgress<CompressionProgress>? progress,
        CancellationToken ct);

    Task<ArchiveContents> InspectAsync(string archivePath, CancellationToken ct);
}
