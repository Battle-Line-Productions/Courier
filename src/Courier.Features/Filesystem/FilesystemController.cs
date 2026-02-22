using Courier.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Filesystem;

[ApiController]
[Route("api/v1/filesystem")]
public class FilesystemController : ControllerBase
{
    private readonly FilesystemService _filesystemService;

    public FilesystemController(FilesystemService filesystemService)
    {
        _filesystemService = filesystemService;
    }

    [HttpGet("browse")]
    public async Task<ActionResult<ApiResponse<BrowseResult>>> Browse(
        [FromQuery] string? path = null,
        CancellationToken ct = default)
    {
        var result = await _filesystemService.BrowseAsync(path);

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
}
