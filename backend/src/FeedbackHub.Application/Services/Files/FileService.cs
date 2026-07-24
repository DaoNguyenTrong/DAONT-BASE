using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Models;
using FeedbackHub.Application.Resources;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Files;

public sealed class FileService(
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    IOptions<StorageSettings> storageOptions) : IFileService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 10;

    private readonly StorageSettings storageSettings = storageOptions.Value;

    public async Task<FileDto> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken)
    {
        ValidateUpload(request);

        StorageUploadResult uploadResult = await storageService.UploadAsync(
            request.Content,
            request.FileName,
            request.ContentType,
            cancellationToken);

        StoredFile storedFile = StoredFile.Create(new StoredFileParams(
            request.FileName,
            request.ContentType,
            uploadResult.Size,
            uploadResult.StoragePath,
            request.OwnerId,
            request.Description,
            request.Category));

        await unitOfWork.Repository<StoredFile, Guid>().AddAsync(storedFile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(storedFile);
    }

    public async Task<FileDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        StoredFile storedFile = await unitOfWork.Repository<StoredFile, Guid>().GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(StoredFile), id);

        return ToDto(storedFile);
    }

    public async Task<PagedResult<FileDto>> GetAllAsync(
        FileListRequest request,
        CancellationToken cancellationToken)
    {
        int pageNumber = request.PageNumber < 1 ? DefaultPageNumber : request.PageNumber;
        int pageSize = request.PageSize < 1 ? DefaultPageSize : request.PageSize;

        (IReadOnlyList<StoredFile> storedFiles, int totalCount) = await unitOfWork.Repository<StoredFile, Guid>()
            .ListPagedAsync(pageNumber, pageSize, cancellationToken);

        return new PagedResult<FileDto>(
            storedFiles.Select(ToDto).ToList(),
            totalCount,
            pageNumber,
            pageSize);
    }

    public async Task<FileDownloadResult> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        StoredFile storedFile = await unitOfWork.Repository<StoredFile, Guid>().GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(StoredFile), id);

        Stream content = await storageService.DownloadAsync(storedFile.StoragePath, cancellationToken);

        return new FileDownloadResult(content, storedFile.FileName, storedFile.ContentType);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        IRepository<StoredFile, Guid> repository = unitOfWork.Repository<StoredFile, Guid>();
        StoredFile storedFile = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(StoredFile), id);

        await storageService.DeleteAsync(storedFile.StoragePath, cancellationToken);

        repository.Delete(storedFile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void ValidateUpload(UploadFileRequest request)
    {
        if (request.Size <= 0)
        {
            throw new DomainException(ApplicationMessages.FileIsRequired);
        }

        if (request.Size > storageSettings.MaxFileSizeBytes)
        {
            throw new FormattedDomainException(
                ApplicationMessages.FileSizeExceeded,
                storageSettings.MaxFileSizeBytes);
        }

        if (storageSettings.AllowedContentTypes.Length > 0 &&
            !storageSettings.AllowedContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException(ApplicationMessages.FileContentTypeNotAllowed);
        }
    }

    private FileDto ToDto(StoredFile storedFile)
    {
        return new FileDto(
            storedFile.Id,
            storedFile.FileName,
            storedFile.ContentType,
            storedFile.Size,
            storedFile.StoragePath,
            BuildPublicUrl(storedFile.StoragePath),
            storedFile.OwnerId,
            storedFile.Description,
            storedFile.Category,
            storedFile.CreatedAt,
            storedFile.UpdatedAt);
    }

    private string BuildPublicUrl(string storagePath)
    {
        string urlBase = storageSettings.PublicUrlBase.TrimEnd('/');

        return $"{urlBase}/{storagePath.TrimStart('/')}";
    }
}
