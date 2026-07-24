using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StarterKit.API.Middleware;

public sealed class UserTimeZoneMiddleware(RequestDelegate next)
{
    private const string TimeZoneHeaderName = "X-TimeZone";
    private const string UserTimeZoneItemKey = "UserTimeZone";
    private const string UserTimeZoneIdItemKey = "UserTimeZoneId";

    private static readonly JsonSerializerSettings ProblemJsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")
            || context.Request.Path.StartsWithSegments("/api/health"))
        {
            await next(context);
            return;
        }

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(TimeZoneHeaderName, out var headerValues)
            || string.IsNullOrWhiteSpace(headerValues.ToString()))
        {
            await WriteProblemAsync(context, $"Missing required header '{TimeZoneHeaderName}'.");
            return;
        }

        var timeZoneId = headerValues.ToString();

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            context.Items[UserTimeZoneItemKey] = timeZone;
            context.Items[UserTimeZoneIdItemKey] = timeZone.Id;
        }
        catch (TimeZoneNotFoundException)
        {
            await WriteProblemAsync(context, $"Invalid timezone id '{timeZoneId}'.");
            return;
        }
        catch (InvalidTimeZoneException)
        {
            await WriteProblemAsync(context, $"Invalid timezone id '{timeZoneId}'.");
            return;
        }

        await next(context);
    }

    private static Task WriteProblemAsync(HttpContext context, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = detail
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsync(JsonConvert.SerializeObject(problemDetails, ProblemJsonSettings));
    }
}
