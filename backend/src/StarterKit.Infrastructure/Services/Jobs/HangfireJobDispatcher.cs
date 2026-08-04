using System.Linq.Expressions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Jobs;

internal sealed class HangfireJobDispatcher(IBackgroundJobClient backgroundJobClient) : IBackgroundJobDispatcher
{
    public void Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall, string queueName)
    {
        Job job = Job.FromExpression(methodCall);
        backgroundJobClient.Create(job, new EnqueuedState(queueName));
    }
}
