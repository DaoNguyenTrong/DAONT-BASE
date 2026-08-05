using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class SystemSettingsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        HttpClient client = fixture.CreateTestClient();
        Account caller;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: $"settings-caller-{Guid.NewGuid():N}", email: $"settings-caller-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, organization.Id));
        return client;
    }

    [Fact]
    public async Task GetAll_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/system-settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_Authenticated_ReturnsOk()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/system-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSection_ThenGetAll_ReflectsNewAndUpdatedValues_ProvingCacheInvalidation()
    {
        HttpClient client = await CreateAuthedClientAsync();
        string prefix = $"test-section-{Guid.NewGuid():N}";

        // Prime the cache with a GetAll before the first write, so a stale-cache bug would surface.
        await client.GetAsync("/api/system-settings");

        HttpResponseMessage firstUpdate = await client.PutAsJsonAsync(
            $"/api/system-settings/{prefix}", new Dictionary<string, string?> { ["key1"] = "value1" });
        Assert.Equal(HttpStatusCode.NoContent, firstUpdate.StatusCode);

        HttpResponseMessage afterFirst = await client.GetAsync("/api/system-settings");
        Dictionary<string, string?>? afterFirstValues = await afterFirst.Content.ReadJsonAsync<Dictionary<string, string?>>();
        Assert.Equal("value1", afterFirstValues![$"{prefix}:key1"]);

        HttpResponseMessage secondUpdate = await client.PutAsJsonAsync(
            $"/api/system-settings/{prefix}",
            new Dictionary<string, string?> { ["key1"] = "value1-updated", ["key2"] = "value2" });
        Assert.Equal(HttpStatusCode.NoContent, secondUpdate.StatusCode);

        HttpResponseMessage afterSecond = await client.GetAsync("/api/system-settings");
        Dictionary<string, string?>? afterSecondValues = await afterSecond.Content.ReadJsonAsync<Dictionary<string, string?>>();
        Assert.Equal("value1-updated", afterSecondValues![$"{prefix}:key1"]);
        Assert.Equal("value2", afterSecondValues[$"{prefix}:key2"]);
    }

    [Fact]
    public async Task UpdateSection_ThenGetAll_ScopedToActiveOrganization_ExcludesOtherOrganizationsValue()
    {
        HttpClient owner = await CreateAuthedClientAsync();
        string prefix = $"test-section-{Guid.NewGuid():N}";
        await owner.PutAsJsonAsync($"/api/system-settings/{prefix}", new Dictionary<string, string?> { ["key1"] = "owner-value" });

        HttpClient outsider = await CreateAuthedClientAsync();
        HttpResponseMessage response = await outsider.GetAsync("/api/system-settings");

        Dictionary<string, string?>? values = await response.Content.ReadJsonAsync<Dictionary<string, string?>>();
        Assert.False(values!.ContainsKey($"{prefix}:key1"));
    }
}
