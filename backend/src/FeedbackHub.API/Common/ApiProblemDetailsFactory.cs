using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using FeedbackHub.Application.Resources;

namespace FeedbackHub.API.Common;

public static class ApiProblemDetailsFactory
{
    private const string ProblemContentType = "application/problem+json";

    public static Task WriteAsync<TProblemDetails>(
        HttpContext context,
        TProblemDetails problemDetails,
        CancellationToken cancellationToken = default)
        where TProblemDetails : ProblemDetails
    {
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return context.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: ProblemContentType,
            cancellationToken: cancellationToken);
    }

    public static CodedProblemDetails Create(
        IStringLocalizer<Messages> localizer,
        int statusCode,
        string title,
        string code,
        object[]? detailArgs = null)
    {
        string detail = detailArgs is { Length: > 0 }
            ? localizer[code, detailArgs].Value
            : localizer[code].Value;

        return new CodedProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Code = code
        };
    }

    public static CodedValidationProblemDetails CreateValidation(
        IStringLocalizer<Messages> localizer,
        IDictionary<string, string[]> errors)
    {
        return new CodedValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = localizer[ApplicationMessages.ValidationFailed].Value,
            Code = ApplicationMessages.ValidationFailed
        };
    }
}
