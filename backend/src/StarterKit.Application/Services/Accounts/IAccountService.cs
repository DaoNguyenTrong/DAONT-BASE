using StarterKit.Application.Common.Models;

namespace StarterKit.Application.Services.Accounts;

public interface IAccountService
{
    Task<PagedResult<AccountDto>> GetAllAsync(
        PaginationRequest request,
        CancellationToken cancellationToken);

    Task<AccountDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken);

    Task<AccountDto> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<ProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken);

    Task<ProfileDto> UpdateCurrentProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken);

    Task ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken);
}
