using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Context;

public sealed class UserTimeZoneProvider(IHttpContextAccessor httpContextAccessor) : IUserTimeZoneProvider
{
    private const string TimeZoneHeaderName = "X-TimeZone";
    private const string UserTimeZoneItemKey = "UserTimeZone";
    private const string UserTimeZoneIdItemKey = "UserTimeZoneId";

    private TimeZoneInfo? userTimeZone;

    public TimeZoneInfo UserTimeZone => userTimeZone ??= ResolveUserTimeZone();

    public string TimeZoneId => UserTimeZone.Id;

    public DateTime ConvertToUtc(DateTime localDateTime)
    {
        var unspecifiedDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(unspecifiedDateTime, UserTimeZone);
    }

    public DateTime ConvertFromUtc(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, UserTimeZone);
    }

    private TimeZoneInfo ResolveUserTimeZone()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return TimeZoneInfo.Utc;
        }

        if (httpContext.Items[UserTimeZoneItemKey] is TimeZoneInfo timeZone)
        {
            return timeZone;
        }

        if (httpContext.Items[UserTimeZoneIdItemKey] is string timeZoneId)
        {
            return FindTimeZoneOrUtc(timeZoneId);
        }

        if (httpContext.Request.Headers.TryGetValue(TimeZoneHeaderName, out var headerValues))
        {
            return FindTimeZoneOrUtc(headerValues.ToString());
        }

        return TimeZoneInfo.Utc;
    }

    private static TimeZoneInfo FindTimeZoneOrUtc(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
