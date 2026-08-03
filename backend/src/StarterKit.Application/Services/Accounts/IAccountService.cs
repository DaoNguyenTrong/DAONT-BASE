namespace StarterKit.Application.Services.Accounts;

public interface IAccountService
{
    Task<ProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken);

    Task<ProfileDto> UpdateCurrentProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken);

    Task ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken);
}
