using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using NSubstitute;
using StarterKit.Application.Services.Notifications;
using StarterKit.Infrastructure.Services.Jobs;

namespace StarterKit.Infrastructure.Tests.Services.Jobs;

public class HangfireJobDispatcherTests
{
    [Fact]
    public void Enqueue_CallsCreateWithExpectedQueue()
    {
        IBackgroundJobClient backgroundJobClient = Substitute.For<IBackgroundJobClient>();
        HangfireJobDispatcher dispatcher = new(backgroundJobClient);
        Guid notificationId = Guid.NewGuid();

        dispatcher.Enqueue<INotificationDispatcher>(
            d => d.DispatchAsync(notificationId, CancellationToken.None), "notifications");

        backgroundJobClient.Received(1).Create(
            Arg.Is<Job>(job => job.Method.Name == nameof(INotificationDispatcher.DispatchAsync)),
            Arg.Is<EnqueuedState>(state => state.Queue == "notifications"));
    }
}
