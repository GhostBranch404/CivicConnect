using System.Net;

namespace CivicConnect.Tests;

public class BlackBoxTestCase
{
    [Fact]
    public async Task GetRequestById_For_A_Request_ThatDoesNotExist()
    {
        await using var factory = new Integration.BlackBoxRequestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/requests/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
