using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Services.Accounts;
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

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = Account.Create(new AccountParams("Password User", "password-change-user", "password-change@example.com"));
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-current", 12));
            account.ConfirmEmail();
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/profile/password", new ChangePasswordRequest("totally-wrong", "newpassword123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    [Fact]
    public async Task ChangePassword_Success_ReturnsNoContent()
    {
        HttpClient client = fixture.CreateTestClient();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = Account.Create(new AccountParams("Password User 2", "password-success-user", "password-success@example.com"));
            account.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("correct-current", 12));
            account.ConfirmEmail();
            context.Accounts.Add(account);
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account));

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/profile/password", new ChangePasswordRequest("correct-current", "newpassword123"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
