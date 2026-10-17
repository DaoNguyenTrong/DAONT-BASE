using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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

    private sealed class OtherAppException(string message) : AppException(message);

    private sealed class RecordingLogger : ILogger<ExceptionHandlingMiddleware>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }

    private static Fixture CreateFixture(RequestDelegate next, ILogger<ExceptionHandlingMiddleware>? logger = null)
    {
        IStringLocalizer<Messages> localizer = Substitute.For<IStringLocalizer<Messages>>();

        localizer[Arg.Any<string>()]
            .Returns(callInfo => new LocalizedString(callInfo.ArgAt<string>(0), $"localized:{callInfo.ArgAt<string>(0)}"));

        localizer[Arg.Any<string>(), Arg.Any<object[]>()]
            .Returns(callInfo => new LocalizedString(
                callInfo.ArgAt<string>(0),
                $"localized:{callInfo.ArgAt<string>(0)}:{string.Join(",", callInfo.ArgAt<object[]>(1))}"));

        ExceptionHandlingMiddleware middleware = new(
            next, logger ?? NullLogger<ExceptionHandlingMiddleware>.Instance, localizer);

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

    [Theory]
    [InlineData("InvokeAsync_ConflictException_Returns409", typeof(ConflictException), StatusCodes.Status409Conflict, "Conflict")]
    [InlineData("InvokeAsync_ForbiddenException_Returns403", typeof(ForbiddenException), StatusCodes.Status403Forbidden, "Forbidden")]
    [InlineData("InvokeAsync_UnauthorizedException_Returns401", typeof(UnauthorizedException), StatusCodes.Status401Unauthorized, "Unauthorized")]
    public async Task InvokeAsync_AppException_MapsToItsStatus(string caseName, Type exceptionType, int status, string title)
    {
        _ = caseName;
        string key = $"{title}.Key";
        Fixture f = CreateFixture(_ => throw (Exception)Activator.CreateInstance(exceptionType, key)!);

        CodedProblemDetails body = await InvokeAndReadBodyAsync(f);

        Assert.Equal(status, f.Context.Response.StatusCode);
        Assert.Equal(title, body.Title);
        Assert.Equal(key, body.Code);
    }

    // A 503 is a dependency being down — worth a warning. A 500 from an AppException means a subtype nobody
    // mapped, our own bug, so it logs at Error like every other 500 and trips the same alerts.
    [Theory]
    [InlineData("InvokeAsync_UnmappedAppException_LogsError", typeof(OtherAppException), LogLevel.Error)]
    [InlineData("InvokeAsync_ServiceUnavailableException_LogsWarning", typeof(ServiceUnavailableException), LogLevel.Warning)]
    public async Task InvokeAsync_ServerSideAppException_LogsAtTheLevelOfItsStatus(
        string caseName, Type exceptionType, LogLevel expected)
    {
        _ = caseName;
        RecordingLogger logger = new();
        Fixture f = CreateFixture(_ => throw (AppException)Activator.CreateInstance(exceptionType, "Message")!, logger);

        await f.Middleware.InvokeAsync(f.Context);

        Assert.Equal([expected], logger.Levels);
    }

    [Fact]
    public async Task InvokeAsync_ClientSideAppException_LogsNothing()
    {
        RecordingLogger logger = new();
        Fixture f = CreateFixture(_ => throw new ConflictException("Conflict"), logger);

        await f.Middleware.InvokeAsync(f.Context);

        Assert.Empty(logger.Levels);
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

    [Fact]
    public async Task InvokeAsync_ClientAbortedRequest_Returns499WithoutBody()
    {
        using CancellationTokenSource aborted = new();
        Fixture f = CreateFixture(_ => throw new OperationCanceledException(aborted.Token));
        aborted.Cancel();
        f.Context.RequestAborted = aborted.Token;

        await f.Middleware.InvokeAsync(f.Context);

        // F5 / changing a filter mid-request is not a server error: no 500, no Error-level log.
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, f.Context.Response.StatusCode);
        Assert.Equal(0, f.Context.Response.Body.Length);
    }

    [Fact]
    public async Task InvokeAsync_CancellationNotCausedByTheClient_StillReturns500()
    {
        // e.g. an internal timeout's CancellationTokenSource — RequestAborted is NOT cancelled.
        Fixture f = CreateFixture(_ => throw new TaskCanceledException("internal timeout"));

        CodedProblemDetails body = await InvokeAndReadBodyAsync(f);

        Assert.Equal(StatusCodes.Status500InternalServerError, f.Context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status500InternalServerError, body.Status);
    }
}
