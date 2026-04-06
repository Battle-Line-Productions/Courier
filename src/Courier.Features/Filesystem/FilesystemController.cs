using Courier.Domain.Common;
using Courier.Domain.Enums;
using Courier.Features.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Filesystem;

[ApiController]
[Route("api/v1/filesystem")]
[Authorize]
public class FilesystemController : ControllerBase
{
    private readonly FilesystemService _filesystemService;

    public FilesystemController(FilesystemService filesystemService)
    {
        _filesystemService = filesystemService;
    }

    [HttpGet("browse")]
    [RequirePermission(Permission.FilesystemBrowse)]
    public async Task<ActionResult<ApiResponse<BrowseResult>>> Browse(
        [FromQuery] string? path = null,
        CancellationToken ct = default)
    {
        var sanitizedPath = SanitizePath(path);
        var result = await _filesystemService.BrowseAsync(sanitizedPath);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.DirectoryNotFound => NotFound(result),
                ErrorCodes.FilesystemAccessDenied => StatusCode(403, result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    private static string? SanitizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return Path.GetFullPath(path);
    }
}
