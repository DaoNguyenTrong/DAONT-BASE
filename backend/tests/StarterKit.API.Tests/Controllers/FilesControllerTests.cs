using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.API.Tests.TestSupport;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.Files;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.API.Tests.Controllers;

[Collection(nameof(ApiCollection))]
public sealed class FilesControllerTests(ApiFactoryFixture fixture) : IAsyncLifetime
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
            caller = await AuthTestHelper.SeedConfirmedAccountAsync(context, username: $"files-caller-{Guid.NewGuid():N}", email: $"files-caller-{Guid.NewGuid():N}@example.com");
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", AuthTestHelper.MintAccessToken(caller));
        return client;
    }

    private static MultipartFormDataContent CreateUploadContent(byte[] bytes, string fileName = "note.txt", string contentType = "text/plain")
    {
        MultipartFormDataContent content = new();
        ByteArrayContent fileContent = new(bytes);
        fileContent.Headers.ContentType = new(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Upload_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = fixture.CreateTestClient();

        HttpResponseMessage response = await client.PostAsync("/api/files", CreateUploadContent("hello"u8.ToArray()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Valid_Returns201WithCorrectSize()
    {
        HttpClient client = await CreateAuthedClientAsync();
        byte[] bytes = "hello world"u8.ToArray();

        HttpResponseMessage response = await client.PostAsync("/api/files", CreateUploadContent(bytes));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        FileDto? dto = await response.Content.ReadJsonAsync<FileDto>();
        Assert.Equal(bytes.Length, dto!.Size);
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.PostAsync("/api/files", CreateUploadContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ExceedsMaxFileSize_ReturnsBadRequest()
    {
        // StorageSettings:MaxFileSizeBytes is 10485760 (10 MB) in the test host's appsettings.json.
        HttpClient client = await CreateAuthedClientAsync();
        byte[] bytes = new byte[10_485_761];

        HttpResponseMessage response = await client.PostAsync("/api/files", CreateUploadContent(bytes));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        HttpClient client = await CreateAuthedClientAsync();

        HttpResponseMessage response = await client.GetAsync($"/api/files/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsPagedFiles()
    {
        HttpClient client = await CreateAuthedClientAsync();
        await client.PostAsync("/api/files", CreateUploadContent("file one"u8.ToArray(), "one.txt"));
        await client.PostAsync("/api/files", CreateUploadContent("file two"u8.ToArray(), "two.txt"));

        HttpResponseMessage response = await client.GetAsync("/api/files?pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedResult<FileDto>? result = await response.Content.ReadJsonAsync<PagedResult<FileDto>>();
        Assert.True(result!.TotalCount >= 2);
    }

    [Fact]
    public async Task Download_ReturnsUploadedBytes()
    {
        HttpClient client = await CreateAuthedClientAsync();
        byte[] bytes = "downloadable content"u8.ToArray();
        HttpResponseMessage uploadResponse = await client.PostAsync("/api/files", CreateUploadContent(bytes, "downloadable.txt"));
        FileDto? uploaded = await uploadResponse.Content.ReadJsonAsync<FileDto>();

        HttpResponseMessage response = await client.GetAsync($"/api/files/{uploaded!.Id}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        byte[] downloaded = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(bytes, downloaded);
    }

    [Fact]
    public async Task Delete_ThenGetById_ReturnsNotFound()
    {
        HttpClient client = await CreateAuthedClientAsync();
        HttpResponseMessage uploadResponse = await client.PostAsync("/api/files", CreateUploadContent("to be deleted"u8.ToArray(), "delete-me.txt"));
        FileDto? uploaded = await uploadResponse.Content.ReadJsonAsync<FileDto>();

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/files/{uploaded!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage getResponse = await client.GetAsync($"/api/files/{uploaded.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
