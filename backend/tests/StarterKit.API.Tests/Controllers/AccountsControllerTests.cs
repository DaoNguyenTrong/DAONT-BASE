using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.Accounts;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class AccountsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        HttpClient client = fixture.CreateTestClient();
        Account caller;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: $"caller-{Guid.NewGuid():N}", email: $"caller-{Guid.NewGuid():N}@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller));
        return client;
    }

    [Fact]
    public async Task GetAll_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_SearchTerm_MatchesAcrossNameUsernameEmail()
    {
        HttpClient client = await CreateAuthedClientAsync();
        await using (AppDbContext context = CreateDbContext())
        {
            await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "findable-widget-account", email: "findable-widget@example.com");
            await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "unrelated-account", email: "unrelated@example.com");
        }

        HttpResponseMessage response = await client.GetAsync("/api/accounts?search=findable-widget&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<AccountDto>? result = await response.Content.ReadJsonAsync<PagedResult<AccountDto>>();
        Assert.Contains(result!.Items, a => a.Username == "findable-widget-account");
        Assert.DoesNotContain(result.Items, a => a.Username == "unrelated-account");
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync($"/api/accounts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Found_ReturnsAccount()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "getbyid-target", email: "getbyid-target@example.com");
        }

        HttpResponseMessage response = await client.GetAsync($"/api/accounts/{account.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AccountDto? dto = await response.Content.ReadJsonAsync<AccountDto>();
        Assert.Equal(account.Id, dto!.Id);
    }

    [Fact]
    public async Task Create_Valid_Returns201WithLocationHeader()
    {
        HttpClient client = await CreateAuthedClientAsync();
        CreateAccountRequest request = new("New Account", null, null, null, "brand-new-account", "brand-new@example.com", "password123");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/accounts", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        AccountDto? dto = await response.Content.ReadJsonAsync<AccountDto>();
        Assert.Equal("brand-new-account", dto!.Username);
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        HttpClient client = await CreateAuthedClientAsync();
        CreateAccountRequest request = new("", null, null, null, "blank-name-account", "blank-name@example.com", "password123");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/accounts", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidEmail_ReturnsBadRequest()
    {
        HttpClient client = await CreateAuthedClientAsync();
        CreateAccountRequest request = new("New Account", null, null, null, "invalid-email-account", "not-an-email", "password123");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/accounts", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // KNOWN GAP: unlike AuthService.RegisterAsync, AccountService.CreateAsync does not
    // pre-check username/email uniqueness before insert. A duplicate hits the DB's unique
    // index and surfaces as an unhandled DbUpdateException -> uncaught by ExceptionHandlingMiddleware's
    // ApiException branch -> 500, not a clean 409. This test documents current behavior, not
    // the desired behavior — flagged separately, not silently fixed as part of test-writing.
    [Fact]
    public async Task Create_DuplicateUsername_CurrentlyReturns500_NotConflict()
    {
        HttpClient client = await CreateAuthedClientAsync();
        await using (AppDbContext context = CreateDbContext())
        {
            await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "dup-account-user", email: "dup1@example.com");
        }
        CreateAccountRequest request = new("New Account", null, null, null, "dup-account-user", "dup2@example.com", "password123");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/accounts", request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Update_NotFound_Returns404()
    {
        HttpClient client = await CreateAuthedClientAsync();
        UpdateAccountRequest request = new("Name", null, null, null, true, "username", "user@example.com");

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/accounts/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_Found_ReturnsUpdatedAccount()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "update-target", email: "update-target@example.com");
        }
        UpdateAccountRequest request = new("Updated Name", null, null, null, true, "update-target", "update-target@example.com");

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/accounts/{account.Id}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AccountDto? dto = await response.Content.ReadJsonAsync<AccountDto>();
        Assert.Equal("Updated Name", dto!.Name);
    }

    [Fact]
    public async Task Update_BlankUsername_ReturnsBadRequest()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "update-validation-target", email: "update-validation-target@example.com");
        }
        UpdateAccountRequest request = new("Updated Name", null, null, null, true, "", "update-validation-target@example.com");

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/accounts/{account.Id}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NotFound_Returns404()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.DeleteAsync($"/api/accounts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Found_ReturnsNoContent()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Account account;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: "delete-target", email: "delete-target@example.com");
        }

        HttpResponseMessage response = await client.DeleteAsync($"/api/accounts/{account.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
