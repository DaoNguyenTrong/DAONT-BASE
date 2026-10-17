using Microsoft.AspNetCore.Http;
using StarterKit.API.Common;
using StarterKit.Domain.Exceptions;

namespace StarterKit.API.Tests.Common;

public class AppExceptionHttpMapperTests
{
    private sealed class OtherAppException(string message) : AppException(message);

    // The first argument keeps each case's former method name in the test report.
    [Theory]
    [InlineData("Map_NotFoundException_ReturnsNotFound", typeof(NotFoundException), StatusCodes.Status404NotFound, "Not Found")]
    [InlineData("Map_ConflictException_ReturnsConflict", typeof(ConflictException), StatusCodes.Status409Conflict, "Conflict")]
    [InlineData("Map_ForbiddenException_ReturnsForbidden", typeof(ForbiddenException), StatusCodes.Status403Forbidden, "Forbidden")]
    [InlineData("Map_UnauthorizedException_ReturnsUnauthorized", typeof(UnauthorizedException), StatusCodes.Status401Unauthorized, "Unauthorized")]
    [InlineData("Map_DomainException_ReturnsBadRequest", typeof(DomainException), StatusCodes.Status400BadRequest, "Bad Request")]
    [InlineData("Map_FormattedDomainException_ReturnsBadRequest", typeof(FormattedDomainException), StatusCodes.Status400BadRequest, "Bad Request")]
    [InlineData("Map_ServiceUnavailableException_ReturnsServiceUnavailable", typeof(ServiceUnavailableException), StatusCodes.Status503ServiceUnavailable, "Service Unavailable")]
    [InlineData("Map_UnmappedAppExceptionSubtype_ReturnsInternalServerError", typeof(OtherAppException), StatusCodes.Status500InternalServerError, "Internal Server Error")]
    public void Map_ReturnsTheStatusAndTitleOfTheExceptionType(
        string caseName,
        Type exceptionType,
        int expectedStatus,
        string expectedTitle)
    {
        _ = caseName;
        // Passed as a Type, not an instance, so every case stays a separately discovered, named test.
        AppException exception = exceptionType == typeof(FormattedDomainException)
            ? new FormattedDomainException("Invalid", "arg")
            : (AppException)Activator.CreateInstance(exceptionType, "Message")!;

        (int statusCode, string title) = AppExceptionHttpMapper.Map(exception);

        Assert.Equal(expectedStatus, statusCode);
        Assert.Equal(expectedTitle, title);
    }
}
