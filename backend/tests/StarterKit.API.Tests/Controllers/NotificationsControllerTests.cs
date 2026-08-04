using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.Notifications;
using StarterKit.Application.Services.Organizations;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class NotificationsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    private AppDbContext CreateDbContext() =>
        fixture.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<(HttpClient Client, Account Account)> CreateAuthedClientAsync()
    {
        HttpClient client = fixture.CreateTestClient();
        Account caller;
        await using (AppDbContext context = CreateDbContext())
        {
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"caller-{Guid.NewGuid():N}", email: $"caller-{Guid.NewGuid():N}@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller, null));
        return (client, caller);
    }

    [Fact]
    public async Task GetAll_NotAuthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_NoNotifications_ReturnsEmptyPagedResult()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<NotificationDto>? result = await response.Content.ReadJsonAsync<PagedResult<NotificationDto>>();
        Assert.Empty(result!.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task AddOrganizationMember_NotifiesTargetAccount()
    {
        (HttpClient ownerClient, Account owner) = await CreateAuthedClientAsync();
        Account target;
        Organization organization;
        Guid memberRoleId;
        await using (AppDbContext context = CreateDbContext())
        {
            organization = await AuthTestHelper.SeedOrganizationAsync(context);
            await AuthTestHelper.SeedOrganizationMemberAsync(context, organization.Id, owner.Id, SystemRoleKind.Owner);
            target = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"target-{Guid.NewGuid():N}", email: $"target-{Guid.NewGuid():N}@example.com");
            memberRoleId = (await AuthTestHelper.SeedSystemRolesAsync(context, organization.Id))[SystemRoleKind.Member].Id;
        }

        HttpResponseMessage addResponse = await ownerClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/members",
            new AddMemberRequest(target.Email, [memberRoleId]),
            JsonTestExtensions.Options);
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        HttpClient targetClient = fixture.CreateTestClient();
        targetClient.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(target, null));

        HttpResponseMessage listResponse = await targetClient.GetAsync("/api/notifications");
        PagedResult<NotificationDto>? result = await listResponse.Content.ReadJsonAsync<PagedResult<NotificationDto>>();
        NotificationDto notification = Assert.Single(result!.Items);
        Assert.Equal(NotificationTypes.OrganizationMemberAdded, notification.Type);
        Assert.Contains(organization.Id.ToString(), notification.Data);
        Assert.False(notification.IsRead);

        HttpResponseMessage countResponse = await targetClient.GetAsync("/api/notifications/unread-count");
        UnreadCountDto? count = await countResponse.Content.ReadJsonAsync<UnreadCountDto>();
        Assert.Equal(1, count!.Count);
    }

    [Fact]
    public async Task MarkAsRead_OwnedNotification_MarksReadAndDropsFromUnreadCount()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        Guid notificationId;
        await using (AppDbContext context = CreateDbContext())
        {
            Notification notification = Notification.Create(
                new NotificationParams(caller.Id, NotificationTypes.OrganizationMemberAdded));
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
            notificationId = notification.Id;
        }

        HttpResponseMessage response = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        HttpResponseMessage countResponse = await client.GetAsync("/api/notifications/unread-count");
        UnreadCountDto? count = await countResponse.Content.ReadJsonAsync<UnreadCountDto>();
        Assert.Equal(0, count!.Count);
    }

    [Fact]
    public async Task MarkAsRead_NotOwnedByCaller_ReturnsNotFound()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        Guid notificationId;
        await using (AppDbContext context = CreateDbContext())
        {
            Account other = await AuthTestHelper.SeedConfirmedAccountAsync(
                context, username: $"other-{Guid.NewGuid():N}", email: $"other-{Guid.NewGuid():N}@example.com");
            Notification notification = Notification.Create(
                new NotificationParams(other.Id, NotificationTypes.OrganizationMemberAdded));
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();
            notificationId = notification.Id;
        }

        HttpResponseMessage response = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksEveryUnreadNotificationForCaller()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        await using (AppDbContext context = CreateDbContext())
        {
            context.Notifications.Add(Notification.Create(
                new NotificationParams(caller.Id, NotificationTypes.OrganizationMemberAdded)));
            context.Notifications.Add(Notification.Create(
                new NotificationParams(caller.Id, NotificationTypes.OrganizationMemberAdded)));
            await context.SaveChangesAsync();
        }

        HttpResponseMessage response = await client.PatchAsync("/api/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        HttpResponseMessage countResponse = await client.GetAsync("/api/notifications/unread-count");
        UnreadCountDto? count = await countResponse.Content.ReadJsonAsync<UnreadCountDto>();
        Assert.Equal(0, count!.Count);
    }
}
