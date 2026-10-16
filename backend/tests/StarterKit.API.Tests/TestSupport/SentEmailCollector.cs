using System.Collections.Concurrent;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.API.Tests.TestSupport;

public sealed record SentEmail(string ToEmail, string Subject, string HtmlBody);

/// <summary>
/// Everything the host tried to email, in order — a singleton so a test can read back what the caller never
/// sees, such as the verification token, which the server stores only as a hash. Cleared by
/// <see cref="ApiFactoryFixture.ResetAsync"/>.
/// </summary>
public sealed class SentEmailCollector
{
    private readonly ConcurrentQueue<SentEmail> sent = new();

    public IReadOnlyList<SentEmail> Sent => [.. sent];

    public void Add(SentEmail email) => sent.Enqueue(email);

    public void Clear() => sent.Clear();
}

/// <summary>Replaces SMTP in the test host: records the message instead of sending it.</summary>
public sealed class CollectingEmailSender(SentEmailCollector collector) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        collector.Add(new SentEmail(toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}
