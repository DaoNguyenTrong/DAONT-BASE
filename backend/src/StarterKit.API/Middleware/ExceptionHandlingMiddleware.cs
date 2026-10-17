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
        catch (AppException exception)
        {
            object[] args = exception switch
            {
                NotFoundException { Args.Length: > 0 } notFoundException => notFoundException.Args,
                FormattedDomainException formattedDomainException => formattedDomainException.Args,
                _ => []
            };

            (int statusCode, string title) = AppExceptionHttpMapper.Map(exception);

            // A 4xx is the caller's mistake and not worth a log line. A 503 is a dependency being down — a warning.
            // A 500 here is an AppException subtype nobody mapped, our own bug: Error, like every other 500, so
            // it trips the same alerts.
            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Request failed with {StatusCode}: {Key}", statusCode, exception.Message);
            }
            else if (statusCode > StatusCodes.Status500InternalServerError)
            {
                logger.LogWarning(exception, "Request failed with {StatusCode}: {Key}", statusCode, exception.Message);
            }

            CodedProblemDetails problemDetails = ApiProblemDetailsFactory.Create(
                localizer,
                statusCode,
                title,
                exception.Message,
                args.Length > 0 ? args : null);

            await ApiProblemDetailsFactory.WriteAsync(context, problemDetails);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away (reload, navigation, a superseded filter request): nobody reads
            // this response, and it's not a server fault — no 500, no Error-level log noise. 499 is
            // nginx's "client closed request" so access logs still tell it apart.
            logger.LogDebug("Request aborted by the client: {Method} {Path}", context.Request.Method, context.Request.Path);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
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
