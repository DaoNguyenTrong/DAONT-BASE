using MailKit.Net.Smtp;

namespace StarterKit.Infrastructure.Services;

internal interface ISmtpClientFactory
{
    ISmtpClient Create();
}

internal sealed class SmtpClientFactory : ISmtpClientFactory
{
    public ISmtpClient Create() => new SmtpClient();
}
