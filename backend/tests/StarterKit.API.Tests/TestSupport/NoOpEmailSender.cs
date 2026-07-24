using StarterKit.Application.Common.Interfaces;

namespace StarterKit.API.Tests.TestSupport;

public sealed class NoOpEmailSender : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
