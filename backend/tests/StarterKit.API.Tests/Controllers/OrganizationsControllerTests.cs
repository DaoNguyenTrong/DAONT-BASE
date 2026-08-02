using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Services.Organizations;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class OrganizationsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<(HttpClient Client, Account Account)> CreateAuthedClientAsync(Guid? organizationId = null)
    {
        HttpClient client = fixture.CreateTestClient();
        Account caller;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"caller-{Guid.NewGuid():N}", email: $"caller-{Guid.NewGuid():N}@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, organizationId));
        return (client, caller);
    }

    [Fact]
    public async Task Create_Valid_Returns201WithCallerAsOwner()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        CreateOrganizationRequest request = new("Acme Inc", $"acme-{Guid.NewGuid():N}");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/organizations", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        OrganizationDto? dto = await response.Content.ReadJsonAsync<OrganizationDto>();
        Assert.Equal(request.Name, dto!.Name);
        Assert.Equal(OrganizationRole.Owner, dto.MyRole);
    }

    [Fact]
    public async Task Create_DuplicateSlug_ReturnsConflict()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        string slug = $"dup-slug-{Guid.NewGuid():N}";
        await using (AppDbContext context = CreateDbContext())
        {
            await AuthTestHelper.SeedOrganizationAsync(context, slug: slug);
        }
        CreateOrganizationRequest request = new("Another Org", slug);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/organizations", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOrganizationsCallerBelongsTo()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization mine;
        await using (AppDbContext context = CreateDbContext())
        {
            mine = await AuthTestHelper.SeedOrganizationAsync(context, name: "Mine");
            await AuthTestHelper.SeedOrganizationMemberAsync(context, mine.Id, caller.Id, OrganizationRole.Admin);

            Organization notMine = await AuthTestHelper.SeedOrganizationAsync(context, name: "NotMine");
            Account otherOwner = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"other-owner-{Guid.NewGuid():N}", email: $"other-owner-{Guid.NewGuid():N}@example.com");
            await AuthTestHelper.SeedOrganizationMemberAsync(context, notMine.Id, otherOwner.Id, OrganizationRole.Owner);
        }

        HttpResponseMessage response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IReadOnlyList<OrganizationDto>? organizations = await response.Content.ReadJsonAsync<List<OrganizationDto>>();
        OrganizationDto only = Assert.Single(organizations!);
        Assert.Equal(mine.Id, only.Id);
        Assert.Equal(OrganizationRole.Admin, only.MyRole);
    }

    [Fact]
    public async Task GetMembers_NotAMember_ReturnsForbidden()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
        }

        HttpResponseMessage response = await client.GetAsync($"/api/organizations/{organization.Id}/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_AsMemberRole_ReturnsForbidden()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        Account target;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, OrganizationRole.Member);
            target = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"target-{Guid.NewGuid():N}", email: $"target-{Guid.NewGuid():N}@example.com");
        }
        AddMemberRequest request = new(target.Email, OrganizationRole.Member);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/members", request, JsonTestExtensions.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_AsOwner_AddsMember()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        Account target;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, OrganizationRole.Owner);
            target = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"target-{Guid.NewGuid():N}", email: $"target-{Guid.NewGuid():N}@example.com");
        }
        AddMemberRequest request = new(target.Email, OrganizationRole.Member);

        HttpResponseMessage addResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/members", request, JsonTestExtensions.Options);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        HttpResponseMessage listResponse = await client.GetAsync($"/api/organizations/{organization.Id}/members");
        IReadOnlyList<OrganizationMemberDto>? members = await listResponse.Content.ReadJsonAsync<List<OrganizationMemberDto>>();
        Assert.Contains(members!, m => m.AccountId == target.Id && m.Role == OrganizationRole.Member);
    }

    [Fact]
    public async Task AddMember_UnknownEmail_ReturnsNotFound()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, OrganizationRole.Owner);
        }
        AddMemberRequest request = new("no-such-account@example.com", OrganizationRole.Member);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/members", request, JsonTestExtensions.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_LastOwner_ReturnsConflict()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, OrganizationRole.Owner);
        }

        HttpResponseMessage response = await client.DeleteAsync($"/api/organizations/{organization.Id}/members/{caller.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_NotLastOwner_RemovesMember()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        Account other;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, OrganizationRole.Owner);
            other = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"other-{Guid.NewGuid():N}", email: $"other-{Guid.NewGuid():N}@example.com");
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, other.Id, OrganizationRole.Member);
        }

        HttpResponseMessage response = await client.DeleteAsync($"/api/organizations/{organization.Id}/members/{other.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_AsNonOwner_ReturnsForbidden()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, OrganizationRole.Admin);
        }

        HttpResponseMessage response = await client.DeleteAsync($"/api/organizations/{organization.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_AsOwner_RevokesSessionAccessToOrganization()
    {
        Account caller;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"caller-{Guid.NewGuid():N}", email: $"caller-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, OrganizationRole.Owner);
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, organization.Id));

        HttpResponseMessage deactivateResponse = await client.DeleteAsync($"/api/organizations/{organization.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        HttpResponseMessage sessionScopedResponse = await client.GetAsync("/api/accounts");
        Assert.Equal(HttpStatusCode.Forbidden, sessionScopedResponse.StatusCode);
    }
}
