using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class CsrfProtectionMiddlewareTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private static HttpRequestMessage CreateUploadRequest(string cookieHeader) =>
        new(HttpMethod.Post, "/api/files")
        {
            Content = CreateUploadContent("hello"u8.ToArray()),
            Headers = { { "Cookie", cookieHeader } }
        };

    private static MultipartFormDataContent CreateUploadContent(byte[] bytes)
    {
        MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(bytes);
        fileContent.Headers.ContentType = new("text/plain");
        content.Add(fileContent, "file", "note.txt");
        return content;
    }

    // Runs ahead of authentication (see Program.cs middleware order) — a forged cross-site form
    // only needs the access_token cookie's *name* to exist, so this must block before the
    // (garbage) JWT is even parsed.
    [Fact]
    public async Task Upload_AccessTokenCookiePresent_NoCsrfHeader_ReturnsForbidden()
    {
        using HttpClient client = fixture.CreateTestClient();
        using HttpRequestMessage request = CreateUploadRequest("access_token=not-a-real-token");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upload_NoAccessTokenCookie_SkipsCsrfCheck_ReachesAuthAndReturnsUnauthorized()
    {
        // No ambient cookie credential to forge — e.g. a Bearer-header or API-key caller — so
        // the CSRF gate must not apply, and the request should fail at authentication instead.
        using HttpClient client = fixture.CreateTestClient();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/files")
        {
            Content = CreateUploadContent("hello"u8.ToArray())
        };

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_AccessTokenCookiePresent_WithAuthorizationHeader_SkipsCsrfCheck()
    {
        // A caller presenting an explicit Bearer credential alongside a stray cookie isn't relying
        // on the ambient cookie, so the CSRF gate should not apply even without the header.
        using HttpClient client = fixture.CreateTestClient();
        using HttpRequestMessage request = CreateUploadRequest("access_token=not-a-real-token");
        request.Headers.Add("Authorization", "Bearer not-a-real-token-either");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ValidAccessTokenCookieWithCsrfHeader_Succeeds()
    {
        using HttpClient client = fixture.CreateTestClient();
        Account caller;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"csrf-caller-{Guid.NewGuid():N}", email: $"csrf-caller-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
        }
        string accessToken = AuthTestHelper.MintAccessToken(caller, organization.Id);

        using HttpRequestMessage request = CreateUploadRequest($"access_token={accessToken}");
        request.Headers.Add("X-CSRF-Protection", "1");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
