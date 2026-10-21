using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Services.Accounts;
using StarterKit.Application.Services.Auth;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class ProfileControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task Get_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ExpiredToken_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();
        AppDbContext context = CreateDbContext();
        Account account = await AuthTestHelper.SeedConfirmedAccountAsync(context);
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintExpiredAccessToken(account));

        HttpResponseMessage response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_TamperedSignature_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();
        AppDbContext context = CreateDbContext();
        Account account = await AuthTestHelper.SeedConfirmedAccountAsync(context);
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintTamperedAccessToken(account));

        HttpResponseMessage response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Authenticated_ReturnsOwnProfile()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "profile-get-user", email: "profile-get@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ProfileDto? dto = await response.Content.ReadJsonAsync<ProfileDto>();
        Assert.Equal(account.Id, dto!.Id);
    }

    [Fact]
    public async Task Update_EmailCollision_ReturnsConflict()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "profile-update-user", email: "profile-update@example.com");
            await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "profile-taken-user", email: "profile-taken@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));
        UpdateProfileRequest request = new("New Name", null, null, null, "profile-taken@example.com");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/profile", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_InvalidEmail_ReturnsBadRequest()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "profile-validation-user", email: "profile-validation@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));
        UpdateProfileRequest request = new("New Name", null, null, null, "not-an-email");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/profile", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_Valid_ReturnsUpdatedProfile()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "profile-valid-user", email: "profile-valid@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));
        UpdateProfileRequest request = new("Updated Name", null, null, null, "profile-valid@example.com");

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/profile", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ProfileDto? dto = await response.Content.ReadJsonAsync<ProfileDto>();
        Assert.Equal("Updated Name", dto!.Name);
    }

    // The first argument keeps each case's former method name in the test report. The short-current case: the
    // current password is only compared against the stored hash — a pre-policy short one must still let its owner
    // move to a compliant password.
    [Theory]
    [InlineData("ChangePassword_WrongCurrentPassword_ReturnsUnauthorized", "correct-current", "totally-wrong", HttpStatusCode.Unauthorized)]
    [InlineData("ChangePassword_Success_ReturnsNoContent", "correct-current", "correct-current", HttpStatusCode.NoContent)]
    [InlineData("ChangePassword_CurrentPasswordShorterThanThePolicy_IsAccepted", "short", "short", HttpStatusCode.NoContent)]
    public async Task ChangePassword_AnswersWithTheStatusOfTheCase(
        string caseName,
        string storedPassword,
        string currentPassword,
        HttpStatusCode expectedStatus)
    {
        _ = caseName;
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = Account.Create(new AccountParams("Password User", "password-change-user", "password-change@example.com"));
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(storedPassword, FastPasswordHasher.WorkFactor));
            account.ConfirmEmail();
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/profile/password", new ChangePasswordRequest(currentPassword, "newpassword123"));

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ShortNewPassword_ReturnsBadRequest()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "password-validation-user", email: "password-validation@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/profile/password", new ChangePasswordRequest("seeded-hash", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }



    // End to end through the real Postgres statements: the current-password check locks after the allowed
    // wrong guesses — even the right one is refused — while sign-in, counted apart, still works.
    [Fact]
    public async Task ChangePassword_AfterTooManyWrongCurrentPasswords_IsLockedButSignInStillWorks()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = Account.Create(new AccountParams("Lockout User", "password-lockout-user", "password-lockout@example.com"));
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-current", FastPasswordHasher.WorkFactor));
            account.ConfirmEmail();
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        for (int attempt = 0; attempt < 5; attempt++)
        {
            HttpResponseMessage wrong = await client.PutAsJsonAsync(
                "/api/profile/password", new ChangePasswordRequest("totally-wrong", "newpassword123"));
            Assert.Equal("InvalidCurrentPassword", await ErrorCodeAsync(wrong));
        }

        HttpResponseMessage locked = await client.PutAsJsonAsync(
            "/api/profile/password", new ChangePasswordRequest("correct-current", "newpassword123"));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("PasswordChangeTemporarilyLocked", await ErrorCodeAsync(locked));

        HttpResponseMessage signIn = await fixture.CreateTestClient().PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("password-lockout-user", "correct-current"));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.TryGetProperty("code", out JsonElement code) ? code.GetString() : null;
    }

    private static MultipartFormDataContent AvatarForm(byte[] bytes, string fileName, string contentType)
    {
        ByteArrayContent file = new(bytes);
        file.Headers.ContentType = new(contentType);

        return new MultipartFormDataContent { { file, "avatar", fileName } };
    }

    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

    [Fact]
    public async Task UploadAvatar_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.PutAsync(
            "/api/profile/avatar", AvatarForm(TinyPng, "a.png", "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAvatar_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.DeleteAsync("/api/profile/avatar");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadAvatar_Png_ReturnsProfileWithAvatarUrl_AndPersistsPath()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "avatar-ok-user", email: "avatar-ok@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.PutAsync(
            "/api/profile/avatar", AvatarForm(TinyPng, "me.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string? url = body.RootElement.GetProperty("avatarUrl").GetString();
        Assert.Contains("/public/avatars/", url);
        await using AppDbContext verify = CreateDbContext();
        Account saved = verify.Accounts.Single(a => a.Id == account.Id);
        Assert.StartsWith("public/avatars/", saved.AvatarPath);
    }

    [Fact]
    public async Task UploadAvatar_NotAnImage_ReturnsBadRequest()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "avatar-bad-user", email: "avatar-bad@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.PutAsync(
            "/api/profile/avatar", AvatarForm("<svg/>"u8.ToArray(), "me.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAvatar_AfterUpload_ClearsAvatarUrl()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "avatar-del-user", email: "avatar-del@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));
        await client.PutAsync("/api/profile/avatar", AvatarForm(TinyPng, "me.png", "image/png"));

        HttpResponseMessage response = await client.DeleteAsync("/api/profile/avatar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("avatarUrl").ValueKind);
    }
}
