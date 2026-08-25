using Microsoft.AspNetCore.Http;
using StarterKit.API.Common;
using StarterKit.Domain.Exceptions;

namespace StarterKit.API.Tests.Common;

public class AppExceptionHttpMapperTests
{
    private sealed class OtherAppException(string message) : AppException(message);

    [Fact]
    public void Map_NotFoundException_ReturnsNotFound()
    {
        (int statusCode, string title) = AppExceptionHttpMapper.Map(new NotFoundException("EntityNotFound"));

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.Equal("Not Found", title);
    }

    [Fact]
    public void Map_ConflictException_ReturnsConflict()
    {
        (int statusCode, string title) = AppExceptionHttpMapper.Map(new ConflictException("Conflict"));

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Equal("Conflict", title);
    }

    [Fact]
    public void Map_ForbiddenException_ReturnsForbidden()
    {
        (int statusCode, string title) = AppExceptionHttpMapper.Map(new ForbiddenException("Forbidden"));

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.Equal("Forbidden", title);
    }

    [Fact]
    public void Map_UnauthorizedException_ReturnsUnauthorized()
    {
        (int statusCode, string title) = AppExceptionHttpMapper.Map(new UnauthorizedException("Unauthorized"));

        Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
        Assert.Equal("Unauthorized", title);
    }

    [Fact]
    public void Map_DomainException_ReturnsBadRequest()
    {
        (int statusCode, string title) = AppExceptionHttpMapper.Map(new DomainException("Invalid"));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Equal("Bad Request", title);
    }

    [Fact]
    public void Map_FormattedDomainException_ReturnsBadRequest()
    {
        (int statusCode, string title) = AppExceptionHttpMapper.Map(new FormattedDomainException("Invalid", "arg"));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Equal("Bad Request", title);
    }

    [Fact]
    public void Map_UnmappedAppExceptionSubtype_ReturnsInternalServerError()
    {
        (int statusCode, string title) = AppExceptionHttpMapper.Map(new OtherAppException("Unmapped"));

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.Equal("Internal Server Error", title);
    }
}
