namespace Courier.Features.Filesystem;

public record BrowseResult
{
    public required string CurrentPath { get; init; }
    public string? ParentPath { get; init; }
    public required IReadOnlyList<FileEntry> Entries { get; init; }
}

public record FileEntry
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public long? Size { get; init; }
    public DateTime? LastModified { get; init; }
}
