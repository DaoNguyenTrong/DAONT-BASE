using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    private static string VerificationToken(SentEmail email) =>
        Regex.Match(email.HtmlBody, "[?&]token=([A-Za-z0-9_-]+)") is { Success: true } match
            ? match.Groups[1].Value
            : throw new InvalidOperationException($"No verification token in the email to {email.ToEmail}.");

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

    // The link in the email is the only place the raw token exists (the server keeps its hash), so this is the
    // one test that proves what is mailed is what verify-email accepts.
    [Fact]
    public async Task Register_Valid_MailsAVerificationLinkWhoseTokenVerifiesTheAccount()
    {
        HttpClient client = fixture.CreateTestClient();
        RegisterRequest request = new("Nguyen Van B", "nvb-register", "nvb-register@example.com", "password123");

        await client.PostAsJsonAsync("/api/auth/register", request);

        SentEmail email = Assert.Single(fixture.Emails.Sent);
        Assert.Equal("nvb-register@example.com", email.ToEmail);
        HttpResponseMessage verify = await client.PostAsJsonAsync(
            "/api/auth/verify-email", new VerifyEmailRequest(VerificationToken(email)));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    // Anyone can register with someone else's address, so the name is attacker-controlled text inside a mail
    // sent from our domain — it must arrive as text, never as markup (a phishing link, say).
    [Fact]
    public async Task Register_NameWithMarkup_IsMailedAsTextNotHtml()
    {
        HttpClient client = fixture.CreateTestClient();
        const string name = "<a href=\"https://evil.example\">Click</a>";

        await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest(name, "markup-name", "markup-name@example.com", "password123"));

        SentEmail email = Assert.Single(fixture.Emails.Sent);
        Assert.DoesNotContain("evil.example\">", email.HtmlBody);
        Assert.Contains("&lt;a href=&quot;https://evil.example&quot;&gt;Click&lt;/a&gt;", email.HtmlBody);
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
    public async Task Register_SameUsernameAndEmailInDifferentCase_ReturnsConflict()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "case-user", email: "case-user@example.com");
        }

        HttpResponseMessage byUsername = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("New Name", "CASE-User", "other@example.com", "password123"));
        HttpResponseMessage byEmail = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("New Name", "other-user", "Case-User@Example.COM", "password123"));

        Assert.Equal(HttpStatusCode.Conflict, byUsername.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, byEmail.StatusCode);
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
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-password", FastPasswordHasher.WorkFactor));
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
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-password", FastPasswordHasher.WorkFactor));
            context.Accounts.Update(account);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("wrong-password-user", "totally-wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // End to end through the real Postgres statements: after the allowed wrong passwords, even the right
    // one is refused with the lock code until the lock runs out.
    [Fact]
    public async Task Login_AfterTooManyWrongPasswords_IsLockedEvenWithTheRightOne()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: "lockout-user", email: "lockout@example.com", passwordHash: null);
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-password", FastPasswordHasher.WorkFactor));
            context.Accounts.Update(account);
            await context.SaveChangesAsync();
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            HttpResponseMessage wrong = await client.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest("lockout-user", "totally-wrong"));
            Assert.Equal("InvalidUsernameOrPassword", await ErrorCodeAsync(wrong));
        }

        HttpResponseMessage locked = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("lockout-user", "correct-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("AccountTemporarilyLocked", await ErrorCodeAsync(locked));
    }

    // Login only compares against the stored hash: an account whose password predates a stricter policy
    // (here: shorter than today's minimum) must still be able to sign in, or a policy change locks it out.
    [Fact]
    public async Task Login_PasswordShorterThanTheCurrentPolicy_StillSignsIn()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: "legacy-password-user", email: "legacy-password@example.com", passwordHash: null);
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("short", FastPasswordHasher.WorkFactor));
            context.Accounts.Update(account);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("legacy-password-user", "short"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndSetsCookies()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: "login-success-user", email: "login-success@example.com", passwordHash: null);
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-password", FastPasswordHasher.WorkFactor));
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
    public async Task Login_UsernameInDifferentCaseAndPadded_Succeeds()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: "mixed-case-user", email: "mixed-case@example.com", passwordHash: null);
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-password", FastPasswordHasher.WorkFactor));
            context.Accounts.Update(account);
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(" Mixed-Case-USER ", "correct-password"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

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

    // Same 204 as a real pending account, and nothing mailed — the response must not reveal which emails exist.
    [Fact]
    public async Task ResendVerification_UnknownEmail_SendsNoEmail()
    {
        HttpClient client = fixture.CreateTestClient();

        await client.PostAsJsonAsync(
            "/api/auth/resend-verification", new ResendVerificationRequest("unknown-nobody@example.com"));

        Assert.Empty(fixture.Emails.Sent);
    }

    [Fact]
    public async Task ResendVerification_PendingAccount_MailsANewLinkThatVerifies()
    {
        HttpClient client = fixture.CreateTestClient();
        await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest("Pending", "resend-pending", "resend-pending@example.com", "password123"));

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/resend-verification", new ResendVerificationRequest("resend-pending@example.com"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(2, fixture.Emails.Sent.Count);
        HttpResponseMessage verify = await client.PostAsJsonAsync(
            "/api/auth/verify-email", new VerifyEmailRequest(VerificationToken(fixture.Emails.Sent[^1])));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
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
    public async Task Refresh_StaleTokenReplayedWithinGrace_StillReturnsNewTokens()
    {
        HttpClient client = fixture.CreateTestClient();
        await using (AppDbContext context = CreateDbContext())
        {
            Account account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: "grace-user", email: "grace-user@example.com");
            context.RefreshTokens.Add(RefreshToken.Create(new RefreshTokenParams(
                account.Id, ComputeSha256("grace-token"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow)));
            await context.SaveChangesAsync();
        }

        // First tab refreshes: "grace-token" is rotated and revoked, a successor in
        // the same family is issued.
        HttpResponseMessage first = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshTokenRequest("grace-token"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second tab, racing with the first, presents the same now-revoked token a
        // moment later — still inside the grace window, so it must succeed too.
        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshTokenRequest("grace-token"));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        LoginResponse? body = await second.Content.ReadJsonAsync<LoginResponse>();
        Assert.NotEqual("grace-token", body!.RefreshToken);
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

    // Check (nginx auth_request for /guide/)

    [Fact]
    public async Task Check_AccessTokenCookie_ReturnsNoContent()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "check-cookie", email: "check-cookie@example.com");
        }

        HttpResponseMessage response = await client.SendAsync(CheckRequest($"access_token={AuthTestHelper.MintAccessToken(account)}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Check_BearerToken_ReturnsNoContent()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "check-bearer", email: "check-bearer@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.GetAsync("/api/auth/check");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Check_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/auth/check");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // An API key belongs to an integration, not a person: it must not open the user guide even though the
    // default policy accepts it everywhere else.
    [Fact]
    public async Task Check_ApiKeyOnly_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            Guid organizationId = (await AuthTestHelper.SeedOrganizationAsync(context)).Id;
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organizationId, "check-api-key");
        }
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.GetAsync("/api/auth/check");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // nginx's subrequest forwards only the browser's cookie, never the app's X-TimeZone header.
    [Fact]
    public async Task Check_WithoutTimeZoneHeader_StillAuthenticatesViaCookie()
    {
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Remove("X-TimeZone");
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "check-no-tz", email: "check-no-tz@example.com");
        }

        HttpResponseMessage withCookie = await client.SendAsync(CheckRequest($"access_token={AuthTestHelper.MintAccessToken(account)}"));
        HttpResponseMessage withoutCookie = await client.GetAsync("/api/auth/check");

        Assert.Equal(HttpStatusCode.NoContent, withCookie.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutCookie.StatusCode);
    }

    private static HttpRequestMessage CheckRequest(string cookie)
    {
        HttpRequestMessage request = new(HttpMethod.Get, "/api/auth/check");
        request.Headers.Add("Cookie", cookie);
        return request;
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

    // A Bearer-only client has no refresh cookie: the session it signed in with is the one its access token names.
    [Fact]
    public async Task RevokeOtherSessions_BearerOnly_KeepsTheSessionTheAccessTokenNames()
    {
        HttpClient client = fixture.CreateTestClient();
        Guid currentFamily = Guid.NewGuid();
        Account owner;
        await using (AppDbContext context = CreateDbContext())
        {
            owner = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "bearer-revoke-user", email: "bearer-revoke@example.com");
            context.RefreshTokens.AddRange(
                RefreshToken.Create(new RefreshTokenParams(owner.Id, ComputeSha256("bearer-current"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow, FamilyId: currentFamily)),
                RefreshToken.Create(new RefreshTokenParams(owner.Id, ComputeSha256("bearer-other"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow)));
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessTokenForSession(owner, currentFamily));

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/sessions/revoke-others", new { });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using AppDbContext verifyContext = CreateDbContext();
        List<RefreshToken> tokens = await verifyContext.RefreshTokens.Where(t => t.AccountId == owner.Id).ToListAsync();
        Assert.Null(tokens.Single(t => t.TokenHash == ComputeSha256("bearer-current")).RevokedAt);
        Assert.NotNull(tokens.Single(t => t.TokenHash == ComputeSha256("bearer-other")).RevokedAt);
    }

    [Fact]
    public async Task GetSessions_BearerOnly_MarksTheSessionTheAccessTokenNamesAsCurrent()
    {
        HttpClient client = fixture.CreateTestClient();
        Guid currentFamily = Guid.NewGuid();
        Account owner;
        await using (AppDbContext context = CreateDbContext())
        {
            owner = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "bearer-list-user", email: "bearer-list@example.com");
            context.RefreshTokens.AddRange(
                RefreshToken.Create(new RefreshTokenParams(owner.Id, ComputeSha256("bearer-list-current"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow, FamilyId: currentFamily)),
                RefreshToken.Create(new RefreshTokenParams(owner.Id, ComputeSha256("bearer-list-other"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow)));
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessTokenForSession(owner, currentFamily));

        HttpResponseMessage response = await client.GetAsync("/api/auth/sessions");

        List<SessionDto>? sessions = await response.Content.ReadJsonAsync<List<SessionDto>>();
        Assert.Equal(2, sessions!.Count);
        Assert.Single(sessions, s => s.IsCurrent);
    }

    // A token minted without a session claim and no refresh cookie names no session. Revoking "the others" then
    // would revoke every session, the caller's included, so the request is refused and nothing is touched.
    [Fact]
    public async Task RevokeOtherSessions_NoIdentifiableSession_ReturnsBadRequestAndRevokesNothing()
    {
        HttpClient client = fixture.CreateTestClient();
        Account owner;
        await using (AppDbContext context = CreateDbContext())
        {
            owner = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "no-session-user", email: "no-session@example.com");
            context.RefreshTokens.Add(
                RefreshToken.Create(new RefreshTokenParams(owner.Id, ComputeSha256("no-session-token"), DateTime.UtcNow.AddDays(1), null, null, false, DateTime.UtcNow)));
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(owner));

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/sessions/revoke-others", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using AppDbContext verifyContext = CreateDbContext();
        RefreshToken token = await verifyContext.RefreshTokens.SingleAsync(t => t.TokenHash == ComputeSha256("no-session-token"));
        Assert.Null(token.RevokedAt);
    }

    // X-Api-Key auth on an [Authorize] endpoint

    [Fact]
    public async Task ApiKeyHeader_AuthenticatesAgainstCombinedPolicy()
    {
        // The API-key handler issues no NameIdentifier or org_id claim, so user- and org-scoped endpoints won't work;
        // PermissionsController.GetAll is an org-independent catalog behind bare [Authorize], so either scheme works.
        HttpClient client = fixture.CreateTestClient();
        Guid organizationId;
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            organizationId = (await AuthTestHelper.SeedOrganizationAsync(context)).Id;
            (_, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, organizationId, "accounts-api-key");
        }
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);

        HttpResponseMessage response = await client.GetAsync("/api/permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private static string ComputeSha256(string input)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
