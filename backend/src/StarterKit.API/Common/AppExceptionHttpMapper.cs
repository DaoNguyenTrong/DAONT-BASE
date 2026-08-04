using StarterKit.Domain.Exceptions;

namespace StarterKit.API.Common;

internal static class AppExceptionHttpMapper
{
    public static (int StatusCode, string Title) Map(AppException exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        DomainException or FormattedDomainException => (StatusCodes.Status400BadRequest, "Bad Request"),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
    };
}
