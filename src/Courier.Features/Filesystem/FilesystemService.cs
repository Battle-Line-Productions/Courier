using Courier.Domain.Common;

namespace Courier.Features.Filesystem;

public class FilesystemService
{
    public Task<ApiResponse<BrowseResult>> BrowseAsync(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return Task.FromResult(BrowseRoots());

            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                return Task.FromResult(new ApiResponse<BrowseResult>
                {
                    Error = ErrorMessages.Create(ErrorCodes.DirectoryNotFound, $"Directory '{path}' not found.")
                });
            }

            var entries = new List<FileEntry>();

            foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
            {
                entries.Add(new FileEntry
                {
                    Name = dir.Name,
                    Type = "directory",
                    LastModified = dir.LastWriteTimeUtc,
                });
            }

            foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
            {
                entries.Add(new FileEntry
                {
                    Name = file.Name,
                    Type = "file",
                    Size = file.Length,
                    LastModified = file.LastWriteTimeUtc,
                });
            }

            var result = new BrowseResult
            {
                CurrentPath = dirInfo.FullName,
                ParentPath = dirInfo.Parent?.FullName,
                Entries = entries,
            };

            return Task.FromResult(new ApiResponse<BrowseResult> { Data = result });
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new ApiResponse<BrowseResult>
            {
                Error = ErrorMessages.Create(ErrorCodes.FilesystemAccessDenied, $"Access denied to '{path}'.")
            });
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult(new ApiResponse<BrowseResult>
            {
                Error = ErrorMessages.Create(ErrorCodes.DirectoryNotFound, $"Directory '{path}' not found.")
            });
        }
    }

    private static ApiResponse<BrowseResult> BrowseRoots()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .OrderBy(d => d.Name)
            .Select(d => new FileEntry
            {
                Name = d.Name,
                Type = "directory",
                Size = d.TotalSize,
            })
            .ToList();

        return new ApiResponse<BrowseResult>
        {
            Data = new BrowseResult
            {
                CurrentPath = "",
                ParentPath = null,
                Entries = drives,
            }
        };
    }
}
