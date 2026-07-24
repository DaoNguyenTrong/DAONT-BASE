using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;

namespace StarterKit.Infrastructure.Services;

public sealed class SmtpEmailSender(
    IOptions<EmailSettings> emailOptions,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailSettings emailSettings = emailOptions.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress(emailSettings.FromName, emailSettings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using SmtpClient client = new();

        try
        {
            await client.ConnectAsync(
                emailSettings.Host,
                emailSettings.Port,
                emailSettings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(emailSettings.Username))
            {
                await client.AuthenticateAsync(emailSettings.Username, emailSettings.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send email to {ToEmail}", toEmail);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }
}
