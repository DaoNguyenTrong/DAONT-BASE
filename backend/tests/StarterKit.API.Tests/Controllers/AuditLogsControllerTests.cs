using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.Auth;
using StarterKit.Application.Services.AuditLogs;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class AuditLogsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
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
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: $"audit-caller-{Guid.NewGuid():N}", email: $"audit-caller-{Guid.NewGuid():N}@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller));
        return client;
    }

    [Fact]
    public async Task GetAll_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RealMutation_ProducesAuditLogEntry()
    {
        HttpClient client = await CreateAuthedClientAsync();
        RegisterRequest registerRequest = new(
            "Audited Account", $"audited-account-user-{Guid.NewGuid():N}", $"audited-account-{Guid.NewGuid():N}@example.com", "password123");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/auth/register", registerRequest);
        RegisterResult? created = await createResponse.Content.ReadJsonAsync<RegisterResult>();

        HttpResponseMessage listResponse = await client.GetAsync("/api/admin/audit-logs?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        PagedResult<AuditLogDto>? auditLogs = await listResponse.Content.ReadJsonAsync<PagedResult<AuditLogDto>>();
        Assert.Contains(auditLogs!.Items, log =>
            log.EntityName == "Account" && log.EntityId == created!.AccountId.ToString() && log.Action == "Added");
    }

    [Fact]
    public async Task GetAll_UserIdFilter_ScopesToThatCaller()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Guid unrelatedUserId = Guid.NewGuid();

        HttpResponseMessage response = await client.GetAsync($"/api/admin/audit-logs?userId={unrelatedUserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<AuditLogDto>? auditLogs = await response.Content.ReadJsonAsync<PagedResult<AuditLogDto>>();
        Assert.Empty(auditLogs!.Items);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/admin/audit-logs/999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Found_ReturnsAuditLog()
    {
        HttpClient client = await CreateAuthedClientAsync();
        RegisterRequest registerRequest = new(
            "GetById Audited", $"getbyid-audited-user-{Guid.NewGuid():N}", $"getbyid-audited-{Guid.NewGuid():N}@example.com", "password123");
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        HttpResponseMessage listResponse = await client.GetAsync("/api/admin/audit-logs?pageSize=100");
        PagedResult<AuditLogDto>? auditLogs = await listResponse.Content.ReadJsonAsync<PagedResult<AuditLogDto>>();
        long targetId = auditLogs!.Items.First(l => l.EntityName == "Account" && l.Action == "Added").Id;

        HttpResponseMessage response = await client.GetAsync($"/api/admin/audit-logs/{targetId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuditLogDto? dto = await response.Content.ReadJsonAsync<AuditLogDto>();
        Assert.Equal(targetId, dto!.Id);
    }
}
