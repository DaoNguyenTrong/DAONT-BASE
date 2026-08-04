using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Models;

namespace StarterKit.Infrastructure.Services.Notifications;

internal sealed class FirebasePushSender(ILogger<FirebasePushSender> logger) : IPushSender
{
    public async Task<PushSendResult> SendAsync(
        IReadOnlyList<string> tokens, PushMessage message, CancellationToken cancellationToken)
    {
#pragma warning disable CS0618 // Tokens is obsolete in favor of Fids (Firebase Installation IDs), which
                               // needs the client-side Firebase Installations SDK — a materially different
                               // integration than the registration-token model this channel is built on.
        MulticastMessage multicastMessage = new()
        {
            Tokens = tokens.ToList(),
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data
        };
#pragma warning restore CS0618

        try
        {
            BatchResponse response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(
                multicastMessage, cancellationToken);

            List<string> invalidTokens = [];
            for (int i = 0; i < response.Responses.Count; i++)
            {
                SendResponse sendResponse = response.Responses[i];
                if (!sendResponse.IsSuccess &&
                    sendResponse.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
                {
                    invalidTokens.Add(tokens[i]);
                }
            }

            return new PushSendResult(invalidTokens, response.SuccessCount, response.FailureCount);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to send push notification via FCM.");
            return new PushSendResult([], 0, tokens.Count);
        }
    }
}
