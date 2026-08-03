using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Common;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Organizations;
using StarterKit.Application.Services.PermissionCatalog;
using StarterKit.Application.Services.Roles;
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
        Assert.Equal(["Owner"], dto.MyRoleNames);
        Assert.Equal(Permissions.All.OrderBy(c => c), dto.MyPermissionCodes.OrderBy(c => c));
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
            await AuthTestHelper.SeedOrganizationMemberAsync(context, mine.Id, caller.Id, SystemRoleKind.Admin);

            Organization notMine = await AuthTestHelper.SeedOrganizationAsync(context, name: "NotMine");
            Account otherOwner = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"other-owner-{Guid.NewGuid():N}", email: $"other-owner-{Guid.NewGuid():N}@example.com");
            await AuthTestHelper.SeedOrganizationMemberAsync(context, notMine.Id, otherOwner.Id, SystemRoleKind.Owner);
        }

        HttpResponseMessage response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IReadOnlyList<OrganizationDto>? organizations = await response.Content.ReadJsonAsync<List<OrganizationDto>>();
        OrganizationDto only = Assert.Single(organizations!);
        Assert.Equal(mine.Id, only.Id);
        Assert.Equal(["Admin"], only.MyRoleNames);
        Assert.Equal(new[] { Permissions.OrganizationMembersManage }, only.MyPermissionCodes);
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

        // Denial now happens in the ASP.NET Core authorization pipeline (AuthorizationResultHandler),
        // not a thrown ForbiddenException caught by ExceptionHandlingMiddleware — assert the body still
        // carries the app's coded/localized shape instead of the framework's default empty 403 body.
        CodedProblemDetails? problem = await response.Content.ReadJsonAsync<CodedProblemDetails>();
        Assert.Equal(ApplicationMessages.OrganizationAccessDenied, problem?.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem?.Detail));
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
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Member);
            target = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"target-{Guid.NewGuid():N}", email: $"target-{Guid.NewGuid():N}@example.com");
        }
        AddMemberRequest request = new(target.Email, [Guid.NewGuid()]);

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
        Guid memberRoleId;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
            target = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"target-{Guid.NewGuid():N}", email: $"target-{Guid.NewGuid():N}@example.com");
            memberRoleId = (await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id))[SystemRoleKind.Member].Id;
        }
        AddMemberRequest request = new(target.Email, [memberRoleId]);

        HttpResponseMessage addResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/members", request, JsonTestExtensions.Options);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        HttpResponseMessage listResponse = await client.GetAsync($"/api/organizations/{organization.Id}/members");
        IReadOnlyList<OrganizationMemberDto>? members = await listResponse.Content.ReadJsonAsync<List<OrganizationMemberDto>>();
        Assert.Contains(members!, m => m.AccountId == target.Id && m.RoleIds.Contains(memberRoleId));
    }

    [Fact]
    public async Task AddMember_UnknownEmail_ReturnsNotFound()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        Guid memberRoleId;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
            memberRoleId = (await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id))[SystemRoleKind.Member].Id;
        }
        AddMemberRequest request = new("no-such-account@example.com", [memberRoleId]);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/members", request, JsonTestExtensions.Options);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMemberRoles_AssignMultipleRoles_Succeeds()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        Account target;
        IReadOnlyDictionary<SystemRoleKind, Role> roles;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
            target = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"target-{Guid.NewGuid():N}", email: $"target-{Guid.NewGuid():N}@example.com");
            roles = await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, target.Id, SystemRoleKind.Member);
        }
        UpdateMemberRolesRequest request = new([roles[SystemRoleKind.Member].Id, roles[SystemRoleKind.Admin].Id]);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/organizations/{organization.Id}/members/{target.Id}", request, JsonTestExtensions.Options);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        HttpResponseMessage listResponse = await client.GetAsync($"/api/organizations/{organization.Id}/members");
        IReadOnlyList<OrganizationMemberDto>? members = await listResponse.Content.ReadJsonAsync<List<OrganizationMemberDto>>();
        OrganizationMemberDto updated = members!.Single(m => m.AccountId == target.Id);
        Assert.Equal(2, updated.RoleIds.Count);
        Assert.Contains(roles[SystemRoleKind.Member].Id, updated.RoleIds);
        Assert.Contains(roles[SystemRoleKind.Admin].Id, updated.RoleIds);
    }

    [Fact]
    public async Task UpdateMemberRoles_DemotingLastOwner_ReturnsConflict()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        IReadOnlyDictionary<SystemRoleKind, Role> roles;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
            roles = await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id);
        }
        UpdateMemberRolesRequest request = new([roles[SystemRoleKind.Admin].Id]);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/organizations/{organization.Id}/members/{caller.Id}", request, JsonTestExtensions.Options);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_LastOwner_ReturnsConflict()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
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
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
            other = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"other-{Guid.NewGuid():N}", email: $"other-{Guid.NewGuid():N}@example.com");
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, other.Id, SystemRoleKind.Member);
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
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Admin);
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
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, organization.Id));

        HttpResponseMessage deactivateResponse = await client.DeleteAsync($"/api/organizations/{organization.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        HttpResponseMessage sessionScopedResponse = await client.GetAsync("/api/accounts");
        Assert.Equal(HttpStatusCode.Forbidden, sessionScopedResponse.StatusCode);
    }

    // Roles

    [Fact]
    public async Task GetRoles_AsActiveMember_ReturnsSeededSystemRoles()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Member);
        }

        HttpResponseMessage response = await client.GetAsync($"/api/organizations/{organization.Id}/roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IReadOnlyList<RoleDto>? roles = await response.Content.ReadJsonAsync<List<RoleDto>>();
        Assert.Equal(3, roles!.Count);
        Assert.All(roles, role => Assert.True(role.IsSystem));
        Assert.Contains(roles, role => role.Name == "Owner" && role.PermissionCodes.OrderBy(c => c).SequenceEqual(Permissions.All.OrderBy(c => c)));
        Assert.Contains(roles, role => role.Name == "Admin" && role.PermissionCodes.Contains(Permissions.OrganizationMembersManage));
        Assert.Contains(roles, role => role.Name == "Member" && role.PermissionCodes.Count == 0);
    }

    [Fact]
    public async Task CreateRole_AsOwner_Returns201WithCustomRole()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
        }
        CreateRoleRequest request = new("Billing Manager", [Permissions.OrganizationMembersManage]);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/roles", request, JsonTestExtensions.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        RoleDto? role = await response.Content.ReadJsonAsync<RoleDto>();
        Assert.Equal("Billing Manager", role!.Name);
        Assert.False(role.IsSystem);
        Assert.Equal([Permissions.OrganizationMembersManage], role.PermissionCodes);
    }

    [Fact]
    public async Task CreateRole_AsMember_ReturnsForbidden()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Member);
        }
        CreateRoleRequest request = new("Billing Manager", []);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/roles", request, JsonTestExtensions.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_SystemRole_ReturnsForbidden()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        IReadOnlyDictionary<SystemRoleKind, Role> roles;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
            roles = await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id);
        }
        UpdateRoleRequest request = new("Renamed Admin", []);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/api/organizations/{organization.Id}/roles/{roles[SystemRoleKind.Admin].Id}", request, JsonTestExtensions.Options);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRole_SystemRole_ReturnsForbidden()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        IReadOnlyDictionary<SystemRoleKind, Role> roles;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
            roles = await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id);
        }

        HttpResponseMessage response = await client.DeleteAsync(
            $"/api/organizations/{organization.Id}/roles/{roles[SystemRoleKind.Member].Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteRole_CustomRole_AsOwner_Succeeds()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, caller.Id, SystemRoleKind.Owner);
        }
        CreateRoleRequest createRequest = new("Temp Role", []);
        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/roles", createRequest, JsonTestExtensions.Options);
        RoleDto customRole = (await createResponse.Content.ReadJsonAsync<RoleDto>())!;

        HttpResponseMessage deleteResponse = await client.DeleteAsync(
            $"/api/organizations/{organization.Id}/roles/{customRole.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GetPermissions_ReturnsFixedCatalog()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/permissions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        IReadOnlyList<PermissionDto>? permissions = await response.Content.ReadJsonAsync<List<PermissionDto>>();
        Assert.Equal(
            Permissions.All.OrderBy(c => c),
            permissions!.Select(p => p.Code).OrderBy(c => c));
    }
}
