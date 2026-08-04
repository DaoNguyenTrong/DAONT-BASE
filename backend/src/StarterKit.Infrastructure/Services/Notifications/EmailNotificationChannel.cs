using Microsoft.Extensions.Logging;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.Notifications;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Infrastructure.Services.Notifications;

internal sealed class EmailNotificationChannel(
    IEmailSender emailSender,
    IUnitOfWork unitOfWork,
    ILogger<EmailNotificationChannel> logger) : INotificationChannel
{
    public string Name => "Email";

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        (string Subject, string HtmlBody)? content =
            NotificationEmailTemplates.TryRender(notification.Type, notification.Data);

        if (content is null)
        {
            logger.LogDebug(
                "No email template registered for notification type {Type} — skipping email channel.",
                notification.Type);
            return;
        }

        Account? account = await unitOfWork.Repository<Account, Guid>()
            .GetByIdAsync(notification.AccountId, cancellationToken);

        if (account is null)
        {
            logger.LogWarning(
                "Account {AccountId} not found — skipping email for notification {NotificationId}",
                notification.AccountId, notification.Id);
            return;
        }

        await emailSender.SendAsync(account.Email, content.Value.Subject, content.Value.HtmlBody, cancellationToken);
    }
}
