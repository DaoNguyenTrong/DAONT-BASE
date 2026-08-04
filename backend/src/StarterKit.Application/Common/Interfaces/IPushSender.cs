using StarterKit.Application.Common.Models;

namespace StarterKit.Application.Common.Interfaces;

public interface IPushSender
{
    Task<PushSendResult> SendAsync(
        IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken);
}
