using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Middleware;

[Collection(nameof(ApiCollection))]
public sealed class TenantAccessMiddlewareTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task OrganizationScopedToken_ActiveMembership_AllowsRequest()
    {
        Account account;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"tenant-ok-{Guid.NewGuid():N}", email: $"tenant-ok-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, account.Id);
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));

        HttpResponseMessage response = await client.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationScopedToken_NoMembershipRow_ReturnsForbidden()
    {
        Account account;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"tenant-revoked-{Guid.NewGuid():N}", email: $"tenant-revoked-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            // No OrganizationMember row seeded — the token claims a tenant the account no longer
            // (or never did) belong to, e.g. removed after the token was issued.
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));

        HttpResponseMessage response = await client.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationScopedToken_RevokedAccess_AuthEndpointStillAllowed()
    {
        Account account;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"tenant-logout-{Guid.NewGuid():N}", email: $"tenant-logout-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            // No membership seeded — access to this org is not (or no longer) valid, but logout
            // must still work so the client can clear its own session state.
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));

        HttpResponseMessage response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationScopedToken_RevokedAccess_OrganizationsEndpointStillAllowed()
    {
        Account account;
        Organization organization;
        await using (AppDbContext context = CreateDbContext())
        {
            account = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"tenant-list-{Guid.NewGuid():N}", email: $"tenant-list-{Guid.NewGuid():N}@example.com");
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            // No membership seeded for `organization` — the token's active org is no longer
            // accessible, but /api/organizations (list-mine, switch targets) must stay reachable
            // so the client can discover other orgs it belongs to and recover.
        }
        HttpClient client = fixture.CreateTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(account, organization.Id));

        HttpResponseMessage response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
