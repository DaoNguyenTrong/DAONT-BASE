namespace StarterKit.Domain.Exceptions;

public static class DomainMessages
{
    public const string AccountNameRequired = nameof(AccountNameRequired);
    public const string AccountUsernameRequired = nameof(AccountUsernameRequired);
    public const string AccountEmailRequired = nameof(AccountEmailRequired);
    public const string PasswordHashRequired = nameof(PasswordHashRequired);
    public const string AccountIdRequired = nameof(AccountIdRequired);
    public const string RefreshTokenRequired = nameof(RefreshTokenRequired);
    public const string RefreshTokenExpiryFuture = nameof(RefreshTokenExpiryFuture);
    public const string FileNameRequired = nameof(FileNameRequired);
    public const string ContentTypeRequired = nameof(ContentTypeRequired);
    public const string FileSizePositive = nameof(FileSizePositive);
    public const string StoragePathRequired = nameof(StoragePathRequired);
    public const string ApiKeyNameRequired = nameof(ApiKeyNameRequired);
    public const string ApiKeyNotFound = nameof(ApiKeyNotFound);
    public const string SystemSettingKeyRequired = nameof(SystemSettingKeyRequired);
    public const string EmailVerificationTokenRequired = nameof(EmailVerificationTokenRequired);
    public const string EmailVerificationTokenExpiryFuture = nameof(EmailVerificationTokenExpiryFuture);
    public const string ExternalLoginProviderRequired = nameof(ExternalLoginProviderRequired);
    public const string ExternalLoginProviderUserIdRequired = nameof(ExternalLoginProviderUserIdRequired);
    public const string OrganizationNameRequired = nameof(OrganizationNameRequired);
    public const string OrganizationSlugRequired = nameof(OrganizationSlugRequired);
    public const string RoleNameRequired = nameof(RoleNameRequired);
    public const string NotificationTypeRequired = nameof(NotificationTypeRequired);
}
