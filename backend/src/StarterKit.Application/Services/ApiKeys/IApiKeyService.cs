namespace StarterKit.Application.Services.ApiKeys;

public interface IApiKeyService
{
    Task<CreateApiKeyResult> CreateAsync(CreateApiKeyRequest request, CancellationToken ct);

    Task<IReadOnlyList<ApiKeyDto>> GetAllAsync(CancellationToken ct);

    Task DeactivateAsync(Guid id, CancellationToken ct);
}
