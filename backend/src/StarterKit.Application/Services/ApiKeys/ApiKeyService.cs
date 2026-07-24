using System.Security.Cryptography;
using System.Text;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.ApiKeys;

public sealed class ApiKeyService(IUnitOfWork unitOfWork) : IApiKeyService
{
    private const int PrefixLength = 8;

    public async Task<CreateApiKeyResult> CreateAsync(CreateApiKeyRequest request, CancellationToken ct)
    {
        string rawKey = GenerateRawKey();
        string prefix = rawKey[..PrefixLength];
        string hash = ComputeSha256(rawKey);

        ApiKey key = ApiKey.Create(new ApiKeyParams(request.Name), prefix, hash);
        await unitOfWork.Repository<ApiKey, Guid>().AddAsync(key, ct);
        await unitOfWork.SaveChangesAsync(ct);

        ApiKeyDto dto = new(key.Id, key.Name, key.IsActive, key.CreatedAt);
        return new CreateApiKeyResult(rawKey, dto);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> GetAllAsync(CancellationToken ct)
    {
        IReadOnlyList<ApiKey> keys = await unitOfWork.Repository<ApiKey, Guid>().ListAsync(ct);
        return keys
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto(k.Id, k.Name, k.IsActive, k.CreatedAt))
            .ToList();
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        ApiKey key = await unitOfWork.Repository<ApiKey, Guid>().GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(ApiKey), id);

        key.Deactivate();
        unitOfWork.Repository<ApiKey, Guid>().Update(key);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static string GenerateRawKey()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return "sk_" + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string ComputeSha256(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
