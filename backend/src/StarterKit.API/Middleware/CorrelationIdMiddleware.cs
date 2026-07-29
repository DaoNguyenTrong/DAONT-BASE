using Serilog.Context;

namespace StarterKit.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context);

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    // Inbound value is untrusted client input that flows straight into log output — only accept
    // it if it looks like an id (alphanumeric/dash/underscore, bounded length), otherwise fall
    // back to ASP.NET Core's own per-request TraceIdentifier rather than reflecting it unchecked.
    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values)
            && values.ToString() is { Length: > 0 and <= MaxLength } candidate
            && IsValidCorrelationId(candidate))
        {
            return candidate;
        }

        return context.TraceIdentifier;
    }

    private static bool IsValidCorrelationId(string value)
    {
        foreach (char c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
