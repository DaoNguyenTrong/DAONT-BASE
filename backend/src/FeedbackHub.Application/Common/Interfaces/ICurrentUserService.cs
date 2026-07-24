namespace FeedbackHub.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }

    string? UserName { get; }

    bool IsApiKeyCaller { get; }

    Guid? ApiKeyId { get; }
}
