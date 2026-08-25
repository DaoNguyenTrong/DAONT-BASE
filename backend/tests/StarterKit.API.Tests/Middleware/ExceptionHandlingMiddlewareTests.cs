using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StarterKit.API.Common;
using StarterKit.API.Middleware;
using StarterKit.Application.Resources;
using StarterKit.Domain.Exceptions;

namespace StarterKit.API.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    // ProblemDetails' base properties serialize lowercase (framework-applied JsonPropertyName
    // attributes); CodedProblemDetails.Code has none and serializes PascalCase — case-insensitive
    // deserialization here matches both without hardcoding the exact mix.
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record Fixture(
        ExceptionHandlingMiddleware Middleware, IStringLocalizer<Messages> Localizer, DefaultHttpContext Context);

    private static Fixture CreateFixture(RequestDelegate next)
    {
        IStringLocalizer<Messages> localizer = Substitute.For<IStringLocalizer<Messages>>();

        localizer[Arg.Any<string>()]
            .Returns(callInfo => new LocalizedString(callInfo.ArgAt<string>(0), $"localized:{callInfo.ArgAt<string>(0)}"));

        localizer[Arg.Any<string>(), Arg.Any<object[]>()]
            .Returns(callInfo => new LocalizedString(
                callInfo.ArgAt<string>(0),
                $"localized:{callInfo.ArgAt<string>(0)}:{string.Join(",", callInfo.ArgAt<object[]>(1))}"));

        ExceptionHandlingMiddleware middleware = new(next, NullLogger<ExceptionHandlingMiddleware>.Instance, localizer);

        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();

        return new Fixture(middleware, localizer, context);
    }

    private static async Task<CodedProblemDetails> InvokeAndReadBodyAsync(Fixture f)
    {
        await f.Middleware.InvokeAsync(f.Context);

        f.Context.Response.Body.Seek(0, SeekOrigin.Begin);
        CodedProblemDetails? body = await JsonSerializer.DeserializeAsync<CodedProblemDetails>(f.Context.Response.Body, DeserializeOptions);

        return body ?? throw new InvalidOperationException("Response body did not deserialize.");
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNextAndLeavesResponseUntouched()
    {
        bool nextCalled = false;
        Fixture f = CreateFixture(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await f.Middleware.InvokeAsync(f.Context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, f.Context.Response.StatusCode);
        Assert.Equal(0, f.Context.Response.Body.Length);
    }

    [Fact]
    public async Task InvokeAsync_NotFoundExceptionMessageOnly_Returns404WithNoArgsPassedToLocalizer()
    {
        Fixture f = CreateFixture(_ => throw new NotFoundException("SomeMessageKey"));

        CodedProblemDetails body = await InvokeAndReadBodyAsync(f);

        Assert.Equal(StatusCodes.Status404NotFound, f.Context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, body.Status);
        Assert.Equal("Not Found", body.Title);
        Assert.Equal("SomeMessageKey", body.Code);
        Assert.Equal("localized:SomeMessageKey", body.Detail);
        _ = f.Localizer.Received(1)["SomeMessageKey"];
        _ = f.Localizer.DidNotReceive()["SomeMessageKey", Arg.Any<object[]>()];
    }

    [Fact]
    public async Task InvokeAsync_NotFoundExceptionWithEntityAndId_Returns404WithArgsPassedToLocalizer()
    {
        Guid missingId = Guid.NewGuid();
        Fixture f = CreateFixture(_ => throw new NotFoundException("Role", missingId));

        CodedProblemDetails body = await InvokeAndReadBodyAsync(f);

        Assert.Equal(StatusCodes.Status404NotFound, body.Status);
        Assert.Equal("EntityNotFound", body.Code);
        Assert.Equal($"localized:EntityNotFound:Role,{missingId}", body.Detail);
        _ = f.Localizer.Received(1)["EntityNotFound", Arg.Is<object[]>(a => a != null && a.Length == 2 && Equals(a[0], "Role") && Equals(a[1], missingId))];
    }

    [Fact]
    public async Task InvokeAsync_ConflictException_Returns409()
    {
        Fixture f = CreateFixture(_ => throw new ConflictException("Conflict.Key"));

        CodedProblemDetails body = await InvokeAndReadBodyAsync(f);

        Assert.Equal(StatusCodes.Status409Conflict, f.Context.Response.StatusCode);
        Assert.Equal("Conflict", body.Title);
        Assert.Equal("Conflict.Key", body.Code);
    }

    [Fact]
    public async Task InvokeAsync_ForbiddenException_Returns403()
    {
        Fixture f = CreateFixture(_ => throw new ForbiddenException("Forbidden.Key"));

        CodedProblemDetails body = await InvokeAndReadBodyAsync(f);

        Assert.Equal(StatusCodes.Status403Forbidden, f.Context.Response.StatusCode);
        Assert.Equal("Forbidden", body.Title);
        Assert.Equal("Forbidden.Key", body.Code);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedException_Returns401()
    {
        Fixture f = CreateFixture(_ => throw new UnauthorizedException("Unauthorized.Key"));

        CodedProblemDetails body = await InvokeAndReadBodyAsync(f);

        Assert.Equal(StatusCodes.Status401Unauthorized, f.Context.Response.StatusCode);
        Assert.Equal("Unauthorized", body.Title);
        Assert.Equal("Unauthorized.Key", body.Code);
    }

    [Fact]
    public async Task InvokeAsync_DomainException_Returns400WithNoArgsPassedToLocalizer()
    {
        Fixture f = CreateFixture(_ => throw new DomainException("Domain.Key"));

        CodedProblemDetails body = await InvokeAndReadBodyAsync(f);

        Assert.Equal(StatusCodes.Status400BadRequest, f.Context.Response.StatusCode);
        Assert.Equal("Bad Request", body.Title);
        Assert.Equal("Domain.Key", body.Code);
        Assert.Equal("localized:Domain.Key", body.Detail);
    }

    [Fact]
    public async Task InvokeAsync_FormattedDomainException_Returns400WithArgsPassedToLocalizer()
    {
        Fixture f = CreateFixture(_ => throw new FormattedDomainException("Formatted.Key", "arg1", 42));

        CodedProblemDetails body = await InvokeAndReadBodyAsync(f);

        Assert.Equal(StatusCodes.Status400BadRequest, body.Status);
        Assert.Equal("Formatted.Key", body.Code);
        Assert.Equal("localized:Formatted.Key:arg1,42", body.Detail);
        _ = f.Localizer.Received(1)["Formatted.Key", Arg.Is<object[]>(a => a != null && a.Length == 2 && Equals(a[0], "arg1") && Equals(a[1], 42))];
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500WithGenericMessage_AndDoesNotLeakOriginalMessage()
    {
        Fixture f = CreateFixture(_ => throw new InvalidOperationException("sensitive internal detail"));

        string rawBody;
        await f.Middleware.InvokeAsync(f.Context);
        f.Context.Response.Body.Seek(0, SeekOrigin.Begin);
        using (StreamReader reader = new(f.Context.Response.Body, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync();
        }
        f.Context.Response.Body.Seek(0, SeekOrigin.Begin);
        CodedProblemDetails body = JsonSerializer.Deserialize<CodedProblemDetails>(rawBody, DeserializeOptions)
            ?? throw new InvalidOperationException("Response body did not deserialize.");

        Assert.Equal(StatusCodes.Status500InternalServerError, f.Context.Response.StatusCode);
        Assert.Equal("Internal Server Error", body.Title);
        Assert.Equal(ApplicationMessages.InternalServerError, body.Code);
        Assert.DoesNotContain("sensitive internal detail", rawBody);
    }

    [Fact]
    public async Task InvokeAsync_AppException_WritesProblemJsonContentType()
    {
        Fixture f = CreateFixture(_ => throw new ConflictException("Conflict.Key"));

        await f.Middleware.InvokeAsync(f.Context);

        Assert.StartsWith("application/problem+json", f.Context.Response.ContentType);
    }
}
