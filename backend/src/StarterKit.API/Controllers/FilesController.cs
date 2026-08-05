using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.Files;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.API.Controllers;

/// <summary>Files scoped to the caller's active organization (JWT <c>org_id</c> claim).</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.ActiveOrganizationMember)]
[Route("api/files")]
public sealed class FilesController(IFileService fileService) : ControllerBase
{
    /// <summary>Uploads a file to the system. Send as multipart/form-data.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FileDto>> Upload(
        IFormFile file,
        string? description,
        string? category,
        CancellationToken cancellationToken)
    {
        await using Stream content = file.OpenReadStream();
        string contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        FileDto storedFile = await fileService.UploadAsync(
            new UploadFileRequest(
                content,
                file.FileName,
                contentType,
                file.Length,
                description,
                category),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = storedFile.Id }, storedFile);
    }

    /// <summary>Returns the metadata of a file by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FileDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        FileDto storedFile = await fileService.GetByIdAsync(id, cancellationToken);

        return Ok(storedFile);
    }

    /// <summary>Returns a paginated list of files for the caller's active organization.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<FileDto>>> GetAll(
        [FromQuery] FileListRequest request,
        CancellationToken cancellationToken)
    {
        PagedResult<FileDto> files = await fileService.GetAllAsync(request, cancellationToken);

        return Ok(files);
    }

    /// <summary>Downloads the content of a file by ID.</summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        FileDownloadResult file = await fileService.DownloadAsync(id, cancellationToken);

        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Deletes a file by ID.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.FilesManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await fileService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
