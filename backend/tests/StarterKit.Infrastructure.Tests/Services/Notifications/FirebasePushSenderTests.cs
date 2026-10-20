using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Microsoft.Extensions.Logging;
using StarterKit.Application.Common.Models;
using StarterKit.Infrastructure.Services.Notifications;

namespace StarterKit.Infrastructure.Tests.Services.Notifications;

/// <summary>
/// <see cref="FirebasePushSender"/> uses <c>FirebaseMessaging.DefaultInstance</c>, so these tests create the default
/// <see cref="FirebaseApp"/> with an offline credential and an <see cref="AppOptions.HttpClientFactory"/> answering FCM
/// sends in-process. That is global state: only this class touches it, serially; <see cref="Dispose"/> deletes it.
/// </summary>
public sealed class FirebasePushSenderTests : IDisposable
{
    private const string ProjectId = "starterkit-test";

    private sealed record Fixture(FirebasePushSender Sender, CapturingLogger Logger, StubFcmHandler Handler);

    public FirebasePushSenderTests() => FirebaseApp.DefaultInstance?.Delete();

    public void Dispose() => FirebaseApp.DefaultInstance?.Delete();

    private static Fixture CreateFixture(bool withFirebaseApp = true)
    {
        StubFcmHandler handler = new();

        if (withFirebaseApp)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromAccessToken("test-access-token"),
                ProjectId = ProjectId,
                HttpClientFactory = new StubHttpClientFactory(handler)
            });
        }

        CapturingLogger logger = new();

        return new Fixture(new FirebasePushSender(logger), logger, handler);
    }

    // SendAsync

    [Fact]
    public async Task SendAsync_AllTokensAccepted_ReportsSuccessWithNoInvalidTokens()
    {
        Fixture f = CreateFixture();

        PushSendResult result = await f.Sender.SendAsync(
            ["token-a", "token-b"], new PushMessage("Title", "Body"), CancellationToken.None);

        Assert.Empty(result.InvalidTokens);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Empty(f.Logger.Errors);
    }

    [Fact]
    public async Task SendAsync_MapsMessageToOneFcmRequestPerToken()
    {
        Fixture f = CreateFixture();
        Dictionary<string, string> data = new() { ["type"] = "OrganizationMemberAdded", ["organizationId"] = "42" };

        await f.Sender.SendAsync(
            ["token-a", "token-b"], new PushMessage("Hello", "World", data), CancellationToken.None);

        Assert.Equal(2, f.Handler.Requests.Count);
        Assert.All(f.Handler.Requests, request =>
        {
            Assert.Equal($"/v1/projects/{ProjectId}/messages:send", request.Path);
            Assert.Equal("Hello", request.Message["notification"]?["title"]?.GetValue<string>());
            Assert.Equal("World", request.Message["notification"]?["body"]?.GetValue<string>());
            Assert.Equal("OrganizationMemberAdded", request.Message["data"]?["type"]?.GetValue<string>());
            Assert.Equal("42", request.Message["data"]?["organizationId"]?.GetValue<string>());
        });
        Assert.Equal(
            ["token-a", "token-b"],
            f.Handler.Requests.Select(r => r.Message["token"]?.GetValue<string>()).Order());
    }

    [Fact]
    public async Task SendAsync_NoData_OmitsDataFromRequest()
    {
        Fixture f = CreateFixture();

        await f.Sender.SendAsync(["token-a"], new PushMessage("Title", "Body"), CancellationToken.None);

        StubFcmHandler.CapturedRequest request = Assert.Single(f.Handler.Requests);
        Assert.Null(request.Message["data"]);
    }

    [Fact]
    public async Task SendAsync_UnregisteredToken_ReportedAsInvalid()
    {
        Fixture f = CreateFixture();
        f.Handler.Unregistered.Add("token-b");

        PushSendResult result = await f.Sender.SendAsync(
            ["token-a", "token-b", "token-c"], new PushMessage("Title", "Body"), CancellationToken.None);

        Assert.Equal(["token-b"], result.InvalidTokens);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
    }

    [Fact]
    public async Task SendAsync_NonUnregisteredFailure_CountedButNotReportedAsInvalid()
    {
        Fixture f = CreateFixture();
        f.Handler.InvalidArgument.Add("token-a");
        f.Handler.Unregistered.Add("token-c");

        PushSendResult result = await f.Sender.SendAsync(
            ["token-a", "token-b", "token-c"], new PushMessage("Title", "Body"), CancellationToken.None);

        Assert.Equal(["token-c"], result.InvalidTokens);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
    }

    [Fact]
    public async Task SendAsync_FirebaseNotInitialized_LogsErrorAndCountsEveryTokenAsFailed()
    {
        Fixture f = CreateFixture(withFirebaseApp: false);

        PushSendResult result = await f.Sender.SendAsync(
            ["token-a", "token-b"], new PushMessage("Title", "Body"), CancellationToken.None);

        Assert.Empty(result.InvalidTokens);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        Assert.Single(f.Logger.Errors);
        Assert.Empty(f.Handler.Requests);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : HttpClientFactory
    {
        protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => handler;
    }

    /// <summary>
    /// Answers FCM v1 <c>messages:send</c> calls. <c>SendEachForMulticastAsync</c> issues one request per
    /// token concurrently, so state is thread-safe and the outcome is keyed on the request's token.
    /// Only 200/400/404 are returned — 429/5xx would make the SDK retry with back-off.
    /// </summary>
    private sealed class StubFcmHandler : HttpMessageHandler
    {
        public sealed record CapturedRequest(string Path, JsonNode Message);

        private readonly ConcurrentQueue<CapturedRequest> requests = new();

        public ConcurrentBag<string> Unregistered { get; } = [];

        public ConcurrentBag<string> InvalidArgument { get; } = [];

        public IReadOnlyCollection<CapturedRequest> Requests => requests;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? "{}"
                : await request.Content.ReadAsStringAsync(cancellationToken);
            JsonNode message = JsonNode.Parse(body)?["message"] ?? new JsonObject();
            requests.Enqueue(new CapturedRequest(request.RequestUri?.AbsolutePath ?? string.Empty, message));

            string token = message["token"]?.GetValue<string>() ?? string.Empty;

            if (Unregistered.Contains(token))
            {
                return Error(HttpStatusCode.NotFound, "NOT_FOUND", "UNREGISTERED");
            }

            if (InvalidArgument.Contains(token))
            {
                return Error(HttpStatusCode.BadRequest, "INVALID_ARGUMENT", "INVALID_ARGUMENT");
            }

            return Json(HttpStatusCode.OK, new { name = $"projects/{ProjectId}/messages/{Guid.NewGuid():N}" });
        }

        private static HttpResponseMessage Error(HttpStatusCode status, string grpcStatus, string fcmErrorCode) =>
            Json(status, new
            {
                error = new
                {
                    code = (int)status,
                    message = fcmErrorCode,
                    status = grpcStatus,
                    details = new object[]
                    {
                        new Dictionary<string, string>
                        {
                            ["@type"] = "type.googleapis.com/google.firebase.fcm.v1.FcmError",
                            ["errorCode"] = fcmErrorCode
                        }
                    }
                }
            });

        private static HttpResponseMessage Json(HttpStatusCode status, object payload) =>
            new(status)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
    }

    private sealed class CapturingLogger : ILogger<FirebasePushSender>
    {
        private readonly ConcurrentQueue<Exception?> errors = new();

        public IReadOnlyCollection<Exception?> Errors => errors;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                errors.Enqueue(exception);
            }
        }
    }
}
