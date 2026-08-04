using System.Linq.Expressions;

namespace StarterKit.Application.Common.Interfaces;

public interface IBackgroundJobDispatcher
{
    void Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall, string queueName);
}
