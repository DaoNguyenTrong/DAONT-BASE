namespace StarterKit.Application.Services.Accounts;

public interface IAccountService
{
    Task<ProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken);

    Task<ProfileDto> UpdateCurrentProfileAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the caller's avatar. The image type comes from the file's bytes, not the client's content type.
    /// </summary>
    Task<ProfileDto> UploadAvatarAsync(AvatarUploadRequest request, CancellationToken cancellationToken);

    Task<ProfileDto> DeleteAvatarAsync(CancellationToken cancellationToken);

    Task ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken);
}
