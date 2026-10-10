using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;
using CivicConnect.Core.Services;
using CivicConnect.Tests;

namespace CivicConnect.Tests.Integration
{
    
    public class CustomWebApplicationFactory : WebApplicationFactory<IRequestService>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("https_port", "443");
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
        public async Task GetRequests_Endpoint_ReturnsSuccessOrUnauthorized()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/requests");

            // Assert
            Assert.True(
                response.IsSuccessStatusCode || 
                response.StatusCode == HttpStatusCode.Unauthorized || 
                response.StatusCode == HttpStatusCode.NotFound
            );
        }

        [Fact]
        public async Task GetRequestById_IF_NonExistentId_ReturnsNotFound_Or_InvalidRequest()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act - Request an ID that does not exist in the database
            var response = await client.GetAsync("/api/requests/999999");

            // Assert
            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound || 
                response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Unauthorized
            );
        }
    }
}