using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StarterKit.API.Middleware;

// A cross-site HTML <form> submission can carry the access_token cookie ambiently but cannot
// attach an arbitrary custom header — that's the entire defense. The header's value is never
// checked, only its presence: unlike a double-submit token, this doesn't need the SPA to read a
// cookie set by the API's own origin (which breaks once frontend/API are split across subdomains).
public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    private const string CsrfHeaderName = "X-CSRF-Protection";
    private const string AccessTokenCookieName = "access_token";

    private static readonly JsonSerializerSettings ProblemJsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")
            || HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method)
            || HttpMethods.IsTrace(context.Request.Method))
        {
            await next(context);
            return;
        }

        bool hasAccessTokenCookie = context.Request.Cookies.ContainsKey(AccessTokenCookieName);
        bool hasExplicitCredential =
            context.Request.Headers.ContainsKey("Authorization") || context.Request.Headers.ContainsKey("X-Api-Key");

        // Only cookie-authenticated requests are forgeable cross-site — a Bearer/API-key caller
        // must set that header explicitly and isn't subject to ambient-credential CSRF.
        if (!hasAccessTokenCookie || hasExplicitCredential)
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.ContainsKey(CsrfHeaderName))
        {
            await WriteProblemAsync(context, $"Missing required header '{CsrfHeaderName}'.");
            return;
        }

        await next(context);
    }

    private static Task WriteProblemAsync(HttpContext context, string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = detail
        };

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsync(JsonConvert.SerializeObject(problemDetails, ProblemJsonSettings));
    }
}
