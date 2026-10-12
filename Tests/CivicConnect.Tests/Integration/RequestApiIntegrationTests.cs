using System.Net;
using System.Net.Http.Json;
using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CivicConnect.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", DatabaseTestHelper.ConnectionString);
        builder.UseEnvironment("Development");
    }
}

public class RequestApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RequestApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostRequest_ReturnsCreatedAndCanBeRetrievedById()
    {
        var scope = await DatabaseTestHelper.CreateScopeAsync();
        try
        {
            var client = CreateClient();
            var payload = DatabaseTestHelper.NewRequest(scope.UserId, scope.CategoryId);

            var createResponse = await client.PostAsJsonAsync("/api/requests", payload);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<CreatedResponse>();
            Assert.NotNull(created);
            Assert.True(created.RequestId > 0);

            var getResponse = await client.GetAsync($"/api/requests/{created.RequestId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var saved = await getResponse.Content.ReadFromJsonAsync<ServiceRequest>();
            Assert.NotNull(saved);
            Assert.Equal(payload.Title, saved.Title);
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(scope);
        }
    }

    [Fact]
    public async Task GetRequests_ReturnsOk()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/requests");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRequestById_NonExistent_ReturnsNotFound()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/api/requests/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchRequestStatus_ReturnsOkAndPersistsChange()
    {
        var scope = await DatabaseTestHelper.CreateScopeAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var requestId = await repository.CreateNewRequest(DatabaseTestHelper.NewRequest(scope.UserId, scope.CategoryId));

            var client = CreateClient();
            var updateBody = new UpdateStatusRequest
            {
                NewStatus = "In Progress",
                ChangedByUserId = scope.UserId,
                ExpectedVersion = 1,
                Notes = "Work started"
            };

            var response = await client.PatchAsJsonAsync($"/api/requests/{requestId}/status", updateBody);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var updated = await repository.GetRequestById(requestId);
            Assert.NotNull(updated);
            Assert.Equal("In Progress", updated.RequestStatus);
            Assert.Equal(2, updated.Version);
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(scope);
        }
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    private sealed class CreatedResponse
    {
        public int RequestId { get; set; }
    }
}
