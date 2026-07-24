using Microsoft.Extensions.Localization;
using StarterKit.API.Common;
using StarterKit.Application.Resources;
using StarterKit.Domain.Exceptions;

namespace StarterKit.API.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IStringLocalizer<Messages> localizer)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException exception)
        {
            object[] args = exception switch
            {
                NotFoundException { Args.Length: > 0 } notFoundException => notFoundException.Args,
                FormattedDomainException formattedDomainException => formattedDomainException.Args,
                _ => []
            };

            CodedProblemDetails problemDetails = ApiProblemDetailsFactory.Create(
                localizer,
                exception.StatusCode,
                exception.Title,
                exception.Message,
                args.Length > 0 ? args : null);

            await ApiProblemDetailsFactory.WriteAsync(context, problemDetails);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception");

            CodedProblemDetails problemDetails = ApiProblemDetailsFactory.Create(
                localizer,
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                ApplicationMessages.InternalServerError);

            await ApiProblemDetailsFactory.WriteAsync(context, problemDetails);
        }
    }
}
