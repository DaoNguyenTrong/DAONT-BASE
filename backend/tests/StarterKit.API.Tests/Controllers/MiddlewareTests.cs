using System.Net;
using StarterKit.API.Tests.TestSupport;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class MiddlewareTests(ApiFactoryFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => fixture.ResetAsync();

    [Fact]
    public async Task MissingTimeZoneHeader_ReturnsBadRequest_BeforeAuthRuns()
    {
        using HttpClient client = fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("X-TimeZone", body);
    }

    [Fact]
    public async Task InvalidTimeZoneHeader_ReturnsBadRequest()
    {
        using HttpClient client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("X-TimeZone", "Not/A_Real_Zone");

        HttpResponseMessage response = await client.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OptionsRequest_BypassesTimeZoneCheck()
    {
        using HttpClient client = fixture.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Options, "/api/accounts");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRequestWithValidTimeZone_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
