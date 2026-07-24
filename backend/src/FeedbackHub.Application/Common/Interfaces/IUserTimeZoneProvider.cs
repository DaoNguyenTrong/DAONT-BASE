namespace FeedbackHub.Application.Common.Interfaces;

public interface IUserTimeZoneProvider
{
    TimeZoneInfo UserTimeZone { get; }

    string TimeZoneId { get; }

    DateTime ConvertToUtc(DateTime localDateTime);

    DateTime ConvertFromUtc(DateTime utcDateTime);
}
