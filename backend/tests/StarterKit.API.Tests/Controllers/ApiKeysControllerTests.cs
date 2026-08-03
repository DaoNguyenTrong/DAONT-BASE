using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Services.ApiKeys;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class ApiKeysControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
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
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: $"apikeys-caller-{Guid.NewGuid():N}", email: $"apikeys-caller-{Guid.NewGuid():N}@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller));
        return client;
    }

    [Fact]
    public async Task GetAll_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_OrdersByCreatedAtDescending()
    {
        HttpClient client = await CreateAuthedClientAsync();
        await using (AppDbContext context = CreateDbContext())
        {
            (_, _) = await AuthTestHelper.SeedActiveApiKeyAsync(context, "Older Key");
            (_, _) = await AuthTestHelper.SeedActiveApiKeyAsync(context, "Newer Key");
        }

        HttpResponseMessage response = await client.GetAsync("/api/admin/api-keys");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<ApiKeyDto>? keys = await response.Content.ReadJsonAsync<List<ApiKeyDto>>();
        int olderIndex = keys!.FindIndex(k => k.Name == "Older Key");
        int newerIndex = keys.FindIndex(k => k.Name == "Newer Key");
        Assert.True(newerIndex < olderIndex);
    }

    [Fact]
    public async Task Create_Valid_Returns201WithRawKeyFormat()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/api-keys", new CreateApiKeyRequest("New CI Key"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        CreateApiKeyResult? result = await response.Content.ReadJsonAsync<CreateApiKeyResult>();
        Assert.Matches(new Regex("^sk_[A-Za-z0-9_-]+$"), result!.RawKey);
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/api-keys", new CreateApiKeyRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_NotFound_Returns404()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.DeleteAsync($"/api/admin/api-keys/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_Found_KeyNoLongerAuthenticates()
    {
        HttpClient client = await CreateAuthedClientAsync();
        ApiKey apiKey;
        string rawKey;
        await using (AppDbContext context = CreateDbContext())
        {
            (apiKey, rawKey) = await AuthTestHelper.SeedActiveApiKeyAsync(context, "Deactivate Target");
        }

        HttpResponseMessage deactivateResponse = await client.DeleteAsync($"/api/admin/api-keys/{apiKey.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        using HttpClient apiKeyClient = fixture.CreateTestClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        HttpResponseMessage authedResponse = await apiKeyClient.GetAsync("/api/admin/api-keys");
        Assert.Equal(HttpStatusCode.Unauthorized, authedResponse.StatusCode);
    }
}
