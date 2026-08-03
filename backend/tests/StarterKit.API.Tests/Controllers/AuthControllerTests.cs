using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Services.Auth;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class AuthControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    // Register

    [Fact]
    public async Task Register_Valid_Returns202WithEmail()
    {
        HttpClient client = fixture.CreateTestClient();
        RegisterRequest request = new("Nguyen Van A", "nva-register", "nva-register@example.com", "password123");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        RegisterResult? result = await response.Content.ReadJsonAsync<RegisterResult>();
        Assert.Equal("nva-register@example.com", result!.Email);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "existing-user", email: "existing@example.com");
        }
        RegisterRequest request = new("New Name", "existing-user", "new@example.com", "password123");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        HttpClient client = fixture.CreateTestClient();
        RegisterRequest request = new("Nguyen Van A", "nva-short", "nva-short@example.com", "short");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Login

    [Fact]
    public async Task Login_UnconfirmedEmail_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            Account account = Account.Create(new AccountParams("Unconfirmed", "unconfirmed-user", "unconfirmed@example.com"));
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-password", 12));
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("unconfirmed-user", "correct-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: "wrong-password-user", email: "wrong-password@example.com", passwordHash: null);
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-password", 12));
            context.Accounts.Update(account);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("wrong-password-user", "totally-wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndSetsCookies()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: "login-success-user", email: "login-success@example.com", passwordHash: null);
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-password", 12));
            context.Accounts.Update(account);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("login-success-user", "correct-password"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        LoginResponse? body = await response.Content.ReadJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        IEnumerable<string> setCookieHeaders = response.Headers.GetValues("Set-Cookie");
        string accessTokenCookie = Assert.Single(setCookieHeaders, h => h.StartsWith("access_token=", StringComparison.Ordinal));
        string refreshTokenCookie = Assert.Single(setCookieHeaders, h => h.StartsWith("refresh_token=", StringComparison.Ordinal));
        foreach (string cookie in new[] { accessTokenCookie, refreshTokenCookie })
        {
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
        }
    }

    // VerifyEmail

    [Fact]
    public async Task VerifyEmail_ValidToken_ReturnsOkWithTokens()
    {
        HttpClient client = fixture.CreateTestClient();
        const string rawToken = "raw-verify-email-token";
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = Account.Create(new AccountParams("Pending", "verify-pending", "verify-pending@example.com"));
            account.SetPasswordHash("hash");
            context.Accounts.Add(account);
            EmailVerificationToken token = EmailVerificationToken.Create(new EmailVerificationTokenParams(
                account.Id, ComputeSha256(rawToken), DateTime.UtcNow.AddHours(1)));
            context.EmailVerificationTokens.Add(token);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/verify-email", new VerifyEmailRequest(rawToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_UnknownToken_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/verify-email", new VerifyEmailRequest("unknown-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ResendVerification

    [Fact]
    public async Task ResendVerification_UnknownEmail_ReturnsNoContentSilently()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/resend-verification", new ResendVerificationRequest("unknown-nobody@example.com"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_AlreadyConfirmed_ReturnsConflict()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: "already-confirmed", email: "already-confirmed@example.com");
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/resend-verification", new ResendVerificationRequest("already-confirmed@example.com"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ExternalLogin

    [Fact]
    public async Task ExternalLogin_UnsupportedProvider_ReturnsBadRequest()
    {
        // ExternalAuthSettings:Google:ClientId is blank in appsettings.json, so no
        // IExternalAuthProvider is registered at all — "google" resolves as unsupported too.
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/external/google", new ExternalLoginRequest("some-credential"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Refresh

    [Fact]
    public async Task Refresh_ViaBodyToken_ReturnsNewTokens()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "refresh-user", email: "refresh-user@example.com");
            RefreshToken token = RefreshToken.Create(new RefreshTokenParams(
                account.Id, ComputeSha256("raw-refresh-token"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow));
            context.RefreshTokens.Add(token);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshTokenRequest("raw-refresh-token"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        LoginResponse? body = await response.Content.ReadJsonAsync<LoginResponse>();
        Assert.NotEqual("raw-refresh-token", body!.RefreshToken);
    }

    [Fact]
    public async Task Refresh_MissingToken_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Logout

    [Fact]
    public async Task Logout_Authenticated_ReturnsNoContent()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "logout-user", email: "logout-user@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(null));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GetSessions / RevokeSession / RevokeOtherSessions

    [Fact]
    public async Task GetSessions_ReturnsOnlyCallersOwnSessions()
    {
        HttpClient client = fixture.CreateTestClient();
        Account owner, other;
        await using (AppDbContext context = CreateDbContext())
        {
            owner = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "sessions-owner", email: "sessions-owner@example.com");
            other = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "sessions-other", email: "sessions-other@example.com");
            context.RefreshTokens.AddRange(
                RefreshToken.Create(new RefreshTokenParams(owner.Id, ComputeSha256("owner-token"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow)),
                RefreshToken.Create(new RefreshTokenParams(other.Id, ComputeSha256("other-token"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow)));
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(owner));

        HttpResponseMessage response = await client.GetAsync("/api/auth/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<SessionDto>? sessions = await response.Content.ReadJsonAsync<List<SessionDto>>();
        Assert.Single(sessions!);
    }

    [Fact]
    public async Task RevokeSession_NotOwned_ReturnsNotFound()
    {
        HttpClient client = fixture.CreateTestClient();
        Account owner, other;
        RefreshToken othersToken;
        await using (AppDbContext context = CreateDbContext())
        {
            owner = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "revoke-owner", email: "revoke-owner@example.com");
            other = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "revoke-other", email: "revoke-other@example.com");
            othersToken = RefreshToken.Create(new RefreshTokenParams(other.Id, ComputeSha256("others-session-token"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow));
            context.RefreshTokens.Add(othersToken);
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(owner));

        HttpResponseMessage response = await client.DeleteAsync($"/api/auth/sessions/{othersToken.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokeOtherSessions_RevokesAllButCurrent()
    {
        HttpClient client = fixture.CreateTestClient();
        Account owner;
        await using (AppDbContext context = CreateDbContext())
        {
            owner = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "revoke-others-user", email: "revoke-others@example.com");
            context.RefreshTokens.AddRange(
                RefreshToken.Create(new RefreshTokenParams(owner.Id, ComputeSha256("current-session"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow)),
                RefreshToken.Create(new RefreshTokenParams(owner.Id, ComputeSha256("other-session"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow)));
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(owner));
        client.DefaultRequestHeaders.Add("Cookie", "refresh_token=current-session");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/sessions/revoke-others", new { });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using AppDbContext verifyContext = CreateDbContext();
        List<RefreshToken> tokens = await verifyContext.RefreshTokens.Where(t => t.AccountId == owner.Id).ToListAsync();
        Assert.Null(tokens.Single(t => t.TokenHash == ComputeSha256("current-session")).RevokedAt);
        Assert.NotNull(tokens.Single(t => t.TokenHash == ComputeSha256("other-session")).RevokedAt);
    }

    // X-Api-Key auth on an [Authorize] endpoint

    [Fact]
    public async Task ApiKeyHeader_AuthenticatesAgainstCombinedPolicy()
    {
        // ApiKeyAuthenticationHandler issues no ClaimTypes.NameIdentifier claim, so a "current
        // user"-scoped endpoint like /sessions can't be used here — GetAll on ApiKeysController
        // only requires [Authorize], not ICurrentUserService.UserId, so it works for either scheme.
        HttpClient client = fixture.CreateTestClient();
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, "accounts-api-key");
        }
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string ComputeSha256(string input)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
