using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Common;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Notifications;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class PushSubscriptionsControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    private const string Route = "/api/push-subscriptions";

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

    private async Task SeedSubscriptionAsync(Guid accountId, string token)
    {
        await using AppDbContext context = CreateDbContext();
        context.PushSubscriptions.Add(PushSubscription.Create(new PushSubscriptionParams(accountId, token, "Web")));
        await context.SaveChangesAsync();
    }

    private async Task<List<PushSubscription>> GetSubscriptionsAsync(string token)
    {
        await using AppDbContext context = CreateDbContext();
        return await context.PushSubscriptions.AsNoTracking().Where(s => s.Token == token).ToListAsync();
    }

    private static string RemoveUrl(string token) => $"{Route}?token={Uri.EscapeDataString(token)}";

    private static async Task<bool> GetIsActiveAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync($"{Route}/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PushSubscriptionStatusResponse? status = await response.Content.ReadJsonAsync<PushSubscriptionStatusResponse>();
        return status!.IsActive;
    }

    // Register

    [Fact]
    public async Task Register_NotAuthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            Route, new RegisterPushSubscriptionRequest("token-1", "Web"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_NewToken_ReturnsNoContentAndStoresSubscription()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            Route, new RegisterPushSubscriptionRequest("token-1", "Web"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        PushSubscription subscription = Assert.Single(await GetSubscriptionsAsync("token-1"));
        Assert.Equal(caller.Id, subscription.AccountId);
        Assert.Equal("Web", subscription.Platform);
        Assert.True(await GetIsActiveAsync(client));
    }

    [Fact]
    public async Task Register_SameTokenTwice_KeepsSingleSubscription()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        RegisterPushSubscriptionRequest request = new("token-1", "Web");

        HttpResponseMessage first = await client.PostAsJsonAsync(Route, request);
        HttpResponseMessage second = await client.PostAsJsonAsync(Route, request);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Single(await GetSubscriptionsAsync("token-1"));
    }

    [Fact]
    public async Task Register_TokenOwnedByAnotherAccount_ReassignsToCaller()
    {
        (HttpClient previousOwnerClient, Account previousOwner) = await CreateAuthedClientAsync();
        (HttpClient callerClient, Account caller) = await CreateAuthedClientAsync();
        await SeedSubscriptionAsync(previousOwner.Id, "shared-device-token");

        HttpResponseMessage response = await callerClient.PostAsJsonAsync(
            Route, new RegisterPushSubscriptionRequest("shared-device-token", "Web"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        PushSubscription subscription = Assert.Single(await GetSubscriptionsAsync("shared-device-token"));
        Assert.Equal(caller.Id, subscription.AccountId);
        Assert.True(await GetIsActiveAsync(callerClient));
        Assert.False(await GetIsActiveAsync(previousOwnerClient));
    }

    [Fact]
    public async Task Register_SamePaddedTokenTwice_KeepsSingleTrimmedSubscription()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        RegisterPushSubscriptionRequest request = new(" padded-token ", "Web");

        HttpResponseMessage first = await client.PostAsJsonAsync(Route, request);
        HttpResponseMessage second = await client.PostAsJsonAsync(Route, request);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Single(await GetSubscriptionsAsync("padded-token"));
    }

    [Fact]
    public async Task Register_PaddedTokenOwnedByAnotherAccount_ReassignsToCaller()
    {
        (_, Account previousOwner) = await CreateAuthedClientAsync();
        (HttpClient callerClient, Account caller) = await CreateAuthedClientAsync();
        await SeedSubscriptionAsync(previousOwner.Id, "shared-device-token");

        HttpResponseMessage response = await callerClient.PostAsJsonAsync(
            Route, new RegisterPushSubscriptionRequest(" shared-device-token ", "Web"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        PushSubscription subscription = Assert.Single(await GetSubscriptionsAsync("shared-device-token"));
        Assert.Equal(caller.Id, subscription.AccountId);
    }

    [Theory]
    [InlineData("", "Web", "Token")]
    [InlineData("token-1", "", "Platform")]
    [InlineData(null, "Web", "Token")]
    [InlineData("token-1", null, "Platform")]
    public async Task Register_MissingField_ReturnsValidationProblem(string? token, string? platform, string field)
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(Route, new { token, platform });

        await AssertValidationProblemAsync(response, field);
    }

    [Theory]
    [InlineData(4097, 3, "Token")]
    [InlineData(10, 21, "Platform")]
    public async Task Register_FieldTooLong_ReturnsValidationProblem(int tokenLength, int platformLength, string field)
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            Route, new RegisterPushSubscriptionRequest(new string('t', tokenLength), new string('p', platformLength)));

        await AssertValidationProblemAsync(response, field);
    }

    [Fact]
    public async Task Register_MaxLengthFields_ReturnsNoContent()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        string token = new('t', 4096);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            Route, new RegisterPushSubscriptionRequest(token, new string('p', 20)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(await GetSubscriptionsAsync(token));
    }

    // Remove

    [Fact]
    public async Task Remove_NotAuthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.DeleteAsync(RemoveUrl("token-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Remove_OwnToken_ReturnsNoContentAndDeletesSubscription()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        await SeedSubscriptionAsync(caller.Id, "token-1");

        HttpResponseMessage response = await client.DeleteAsync(RemoveUrl("token-1"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetSubscriptionsAsync("token-1"));
        Assert.False(await GetIsActiveAsync(client));
    }

    [Fact]
    public async Task Remove_PaddedOwnToken_DeletesSubscription()
    {
        (HttpClient client, Account caller) = await CreateAuthedClientAsync();
        await SeedSubscriptionAsync(caller.Id, "token-1");

        HttpResponseMessage response = await client.DeleteAsync(RemoveUrl(" token-1 "));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await GetSubscriptionsAsync("token-1"));
    }

    [Fact]
    public async Task Remove_AnotherAccountsToken_ReturnsNoContentAndKeepsSubscription()
    {
        (HttpClient ownerClient, Account owner) = await CreateAuthedClientAsync();
        (HttpClient otherClient, _) = await CreateAuthedClientAsync();
        await SeedSubscriptionAsync(owner.Id, "owner-token");

        HttpResponseMessage response = await otherClient.DeleteAsync(RemoveUrl("owner-token"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        PushSubscription subscription = Assert.Single(await GetSubscriptionsAsync("owner-token"));
        Assert.Equal(owner.Id, subscription.AccountId);
        Assert.True(await GetIsActiveAsync(ownerClient));
    }

    [Fact]
    public async Task Remove_UnknownToken_ReturnsNoContent()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.DeleteAsync(RemoveUrl("never-registered"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Remove_MissingToken_ReturnsValidationProblem()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.DeleteAsync(Route);

        await AssertValidationProblemAsync(response, "token");
    }

    // GetStatus

    [Fact]
    public async Task GetStatus_NotAuthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync($"{Route}/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetStatus_NoSubscription_ReturnsInactive()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();

        Assert.False(await GetIsActiveAsync(client));
    }

    [Fact]
    public async Task GetStatus_OnlyAnotherAccountSubscribed_ReturnsInactive()
    {
        (HttpClient client, _) = await CreateAuthedClientAsync();
        (_, Account other) = await CreateAuthedClientAsync();
        await SeedSubscriptionAsync(other.Id, "other-token");

        Assert.False(await GetIsActiveAsync(client));
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        CodedProblemDetails? problem = await response.Content.ReadJsonAsync<CodedProblemDetails>();
        Assert.Equal(ApplicationMessages.ValidationFailed, problem?.Code);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(
            body.RootElement.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, field, StringComparison.OrdinalIgnoreCase));
    }
}
