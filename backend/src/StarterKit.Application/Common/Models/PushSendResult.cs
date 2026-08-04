namespace StarterKit.Application.Common.Models;

public record PushSendResult(IReadOnlyList<string> InvalidTokens, int SuccessCount, int FailureCount);
