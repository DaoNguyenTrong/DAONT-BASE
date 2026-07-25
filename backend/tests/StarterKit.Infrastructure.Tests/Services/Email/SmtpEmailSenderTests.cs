using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Services.Email;

namespace StarterKit.Infrastructure.Tests.Services.Email;

public class SmtpEmailSenderTests
{
    private sealed record Fixture(SmtpEmailSender Sender, ISmtpClient Client);

    private static Fixture CreateFixture(string username = "", bool useSsl = false)
    {
        IOptions<EmailSettings> options = Options.Create(new EmailSettings
        {
            Host = "smtp.example.com",
            Port = 587,
            Username = username,
            Password = "password",
            FromAddress = "noreply@example.com",
            FromName = "StarterKit",
            UseSsl = useSsl
        });

        ISmtpClient client = Substitute.For<ISmtpClient>();
        ISmtpClientFactory clientFactory = Substitute.For<ISmtpClientFactory>();
        clientFactory.Create().Returns(client);

        SmtpEmailSender sender = new(options, NullLogger<SmtpEmailSender>.Instance, clientFactory);

        return new Fixture(sender, client);
    }

    [Fact]
    public async Task SendAsync_ConnectsWithConfiguredHostAndPort()
    {
        Fixture f = CreateFixture();

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.Received(1).ConnectAsync(
            "smtp.example.com", 587, Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UseSslTrue_ConnectsWithSslOnConnect()
    {
        Fixture f = CreateFixture(useSsl: true);

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.Received(1).ConnectAsync(
            Arg.Any<string>(), Arg.Any<int>(), SecureSocketOptions.SslOnConnect, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_BlankUsername_SkipsAuthenticate()
    {
        Fixture f = CreateFixture(username: "");

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.DidNotReceive().AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_NonBlankUsername_Authenticates()
    {
        Fixture f = CreateFixture(username: "smtp-user");

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.Received(1).AuthenticateAsync("smtp-user", "password", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SendsMessageWithSubjectAndRecipient()
    {
        Fixture f = CreateFixture();

        await f.Sender.SendAsync("to@example.com", "Welcome", "<p>Hello</p>", CancellationToken.None);

        await f.Client.Received(1).SendAsync(
            Arg.Is<MimeMessage>(m => m != null
                && m.Subject == "Welcome"
                && m.To.Mailboxes.Any(mb => mb.Address == "to@example.com")),
            Arg.Any<CancellationToken>(),
            Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_Connected_DisconnectsInFinally()
    {
        Fixture f = CreateFixture();
        f.Client.IsConnected.Returns(true);

        await f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None);

        await f.Client.Received(1).DisconnectAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SendFails_DisconnectsAndRethrows()
    {
        Fixture f = CreateFixture();
        f.Client.IsConnected.Returns(true);
        f.Client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<string>(_ => throw new InvalidOperationException("smtp failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Sender.SendAsync("to@example.com", "Subject", "<p>Body</p>", CancellationToken.None));

        await f.Client.Received(1).DisconnectAsync(true, Arg.Any<CancellationToken>());
    }
}
