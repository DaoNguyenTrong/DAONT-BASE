namespace StarterKit.Application.Services.Accounts;

public sealed record AvatarUploadRequest(Stream Content, string ContentType, long Size);
