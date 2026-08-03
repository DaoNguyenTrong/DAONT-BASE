using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Localization;
using StarterKit.API.Common;
using StarterKit.Application.Resources;

namespace StarterKit.API.Authorization;

/// <summary>
/// Overrides only the Forbidden outcome of the framework's default authorization result handler,
/// so a policy denial still produces the app's <see cref="CodedProblemDetails"/> body (same shape
/// <see cref="Middleware.ExceptionHandlingMiddleware"/> writes for a thrown <c>ForbiddenException</c>)
/// instead of the framework's empty-body 403 — no <c>AddProblemDetails()</c> is registered, so
/// leaving this to the default handler would silently drop the localized error message the
/// frontend displays. Challenge (401) and Success are delegated to the real handler untouched.
/// </summary>
internal sealed class AuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler Default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await Default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        IStringLocalizer<Messages> localizer =
            context.RequestServices.GetRequiredService<IStringLocalizer<Messages>>();

        CodedProblemDetails problemDetails = ApiProblemDetailsFactory.Create(
            localizer,
            StatusCodes.Status403Forbidden,
            "Forbidden",
            ApplicationMessages.OrganizationAccessDenied);

        await ApiProblemDetailsFactory.WriteAsync(context, problemDetails, context.RequestAborted);
    }
}
