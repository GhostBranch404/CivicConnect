using System.Net;
using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicConnect.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<IRequestService>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=not-used;");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRequestService>();
            services.AddSingleton<IRequestService>(new StubRequestService());
        });
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
    public async Task GetRequests_Endpoint_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/request");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRequestById_WhenRequestDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/request/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

internal sealed class StubRequestService : IRequestService
{
    public Task<int> RequestSubmission(ServiceRequest request) =>
        throw new NotSupportedException();

    public Task<ServiceRequest?> GetRequestById(int id) =>
        Task.FromResult<ServiceRequest?>(null);

    public Task<IEnumerable<ServiceRequest>> GetAllRequests() =>
        Task.FromResult<IEnumerable<ServiceRequest>>(Array.Empty<ServiceRequest>());

    public Task<bool> UpdateRequestStatus(
        int id,
        string newStatus,
        int changedByUserId,
        int expectedVersion,
        string? notes = null) =>
        throw new NotSupportedException();
}
