using StarterKit.API.Middleware;
using StarterKit.API.Tests.TestSupport;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class CorrelationIdMiddlewareTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    [Fact]
    public async Task ValidInboundCorrelationId_IsEchoedBack()
    {
        using HttpClient client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "client-generated-id_123");

        HttpResponseMessage response = await client.GetAsync("/api/health");

        Assert.Equal("client-generated-id_123", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task InboundCorrelationIdWithDisallowedCharacters_IsReplacedNotReflected()
    {
        // CRLF itself is already rejected by HttpClient/Kestrel at the transport level before it
        // reaches any middleware — this exercises the allowlist against transport-legal junk
        // (spaces/symbols) that would otherwise be reflected verbatim into every log line.
        using HttpClient client = fixture.CreateClient();
        const string junk = "not a valid id! (has spaces & symbols)";
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, junk);

        HttpResponseMessage response = await client.GetAsync("/api/health");

        string returned = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.NotEqual(junk, returned);
        Assert.False(string.IsNullOrWhiteSpace(returned));
    }

    [Fact]
    public async Task InboundCorrelationIdExceedingMaxLength_IsReplacedNotReflected()
    {
        using HttpClient client = fixture.CreateClient();
        string tooLong = new string('a', 65);
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, tooLong);

        HttpResponseMessage response = await client.GetAsync("/api/health");

        string returned = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.NotEqual(tooLong, returned);
    }

    [Fact]
    public async Task NoInboundCorrelationId_StillReturnsGeneratedId()
    {
        using HttpClient client = fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/health");

        string returned = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        Assert.False(string.IsNullOrWhiteSpace(returned));
    }
}
