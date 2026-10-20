using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Localization;
using StarterKit.API.Common;
using StarterKit.Application.Resources;

namespace StarterKit.API.Authorization;

/// <summary>
/// Overrides only Forbidden (Challenge/401 and Success keep the default handler), giving a policy denial the same
/// <see cref="CodedProblemDetails"/> body <see cref="Middleware.ExceptionHandlingMiddleware"/> writes for a
/// <c>ForbiddenException</c>; with no <c>AddProblemDetails()</c>, the default empty 403 drops the localized message.
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
