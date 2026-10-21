using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Middleware;

/// <summary>
/// The fallback policy denies anything that declares no access; the endpoints that are meant to be anonymous
/// must opt out explicitly, or they turn into 401s.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class FallbackAuthorizationTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    // Fallback policy
    [Fact]
    public async Task UnmappedApiRoute_WithoutToken_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnmappedApiRoute_WithToken_ReturnsNotFound()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "fallback-user", email: "fallback@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // UseCors answers a preflight before UseAuthorization, so the fallback never sees it.
    [Fact]
    public async Task CorsPreflight_WithoutToken_IsAnsweredByCors()
    {
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Options, "/api/admin/api-keys");
        request.Headers.Add("Origin", "http://localhost:5175");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:5175", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    // Anonymous endpoints outside the controllers
    [Theory]
    [InlineData("/metrics")]
    [InlineData("/hangfire")]
    [InlineData("/hangfire/recurring")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    public async Task OperationalEndpoint_WithoutToken_IsServed(string path)
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
