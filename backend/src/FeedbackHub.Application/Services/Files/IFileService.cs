using FeedbackHub.Application.Common.Models;

namespace FeedbackHub.Application.Services.Files;

public interface IFileService
{
    Task<FileDto> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken);

    Task<FileDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<FileDto>> GetAllAsync(FileListRequest request, CancellationToken cancellationToken);

    Task<FileDownloadResult> DownloadAsync(Guid id, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
