using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Context;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
