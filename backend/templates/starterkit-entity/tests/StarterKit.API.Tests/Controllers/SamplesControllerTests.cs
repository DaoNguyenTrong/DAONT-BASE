using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Services.Samples;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class SamplesControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<HttpClient> CreateClientAsync(SystemRoleKind role)
    {
        HttpClient client = fixture.CreateTestClient();
        Account caller;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(
                context,
                username: $"samples-caller-{Guid.NewGuid():N}",
                email: $"samples-caller-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, role);
        }

        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, organization.Id));
        return client;
    }

    // GetAll

    [Fact]
    public async Task GetAll_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/samples");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutPermission_ReturnsForbidden()
    {
        HttpClient client = await CreateClientAsync(SystemRoleKind.Member);

        HttpResponseMessage response = await client.GetAsync("/api/samples");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Create

    [Fact]
    public async Task Create_Valid_Returns201AndTheRowIsListed()
    {
        HttpClient client = await CreateClientAsync(SystemRoleKind.Owner);

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/samples", new SampleRequest("First"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        SampleDto? dto = await created.Content.ReadJsonAsync<SampleDto>();
        HttpResponseMessage listed = await client.GetAsync("/api/samples");
        List<SampleDto>? items = await listed.Content.ReadJsonAsync<List<SampleDto>>();
        Assert.Contains(items!, item => item.Id == dto!.Id && item.Name == "First");
    }

    [Fact]
    public async Task Create_BlankName_ReturnsBadRequest()
    {
        HttpClient client = await CreateClientAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/samples", new SampleRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Update

    [Fact]
    public async Task Update_Valid_RenamesTheRow()
    {
        HttpClient client = await CreateClientAsync(SystemRoleKind.Owner);
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/samples", new SampleRequest("First"));
        SampleDto? dto = await created.Content.ReadJsonAsync<SampleDto>();

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/samples/{dto!.Id}", new SampleRequest("Renamed"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        SampleDto? updated = await response.Content.ReadJsonAsync<SampleDto>();
        Assert.Equal("Renamed", updated!.Name);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsNotFound()
    {
        HttpClient client = await CreateClientAsync(SystemRoleKind.Owner);

        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/samples/{Guid.NewGuid()}", new SampleRequest("Renamed"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Delete

    [Fact]
    public async Task Delete_Existing_Returns204ThenNotFound()
    {
        HttpClient client = await CreateClientAsync(SystemRoleKind.Owner);
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/samples", new SampleRequest("First"));
        SampleDto? dto = await created.Content.ReadJsonAsync<SampleDto>();

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/samples/{dto!.Id}");
        HttpResponseMessage again = await client.GetAsync($"/api/samples/{dto.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }
//#if (OrgScoped)

    [Fact]
    public async Task GetById_OtherOrganizationsRow_ReturnsNotFound()
    {
        HttpClient owner = await CreateClientAsync(SystemRoleKind.Owner);
        HttpClient stranger = await CreateClientAsync(SystemRoleKind.Owner);
        HttpResponseMessage created = await owner.PostAsJsonAsync("/api/samples", new SampleRequest("Private"));
        SampleDto? dto = await created.Content.ReadJsonAsync<SampleDto>();

        HttpResponseMessage response = await stranger.GetAsync($"/api/samples/{dto!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
//#endif
}
