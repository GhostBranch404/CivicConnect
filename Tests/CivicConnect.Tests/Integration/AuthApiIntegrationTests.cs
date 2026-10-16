using System.Net;
using System.Net.Http.Json;
using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicConnect.Tests.Integration;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    public FakeConfirmationRepository Users { get; } = new();
    public SwitchableEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=not-used;");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserConfirmationRepository>();
            services.AddSingleton<IUserConfirmationRepository>(Users);
            services.RemoveAll<IEmailConfirmationSender>();
            services.AddSingleton<IEmailConfirmationSender>(EmailSender);
            services.RemoveAll<IDeveloperLogRepository>();
            services.AddSingleton<IDeveloperLogRepository, NullDeveloperLogRepository>();
        });
    }
}

public sealed class AuthApiIntegrationTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuthApiIntegrationTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    [Fact]
    public async Task RegisterAndConfirmEmail_ReturnsCreatedThenSuccess()
    {
        using var client = Client();
        _factory.EmailSender.Fail = false;

        using var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = "Integration User",
            Password = "correct-horse-battery",
            Email = "integration@example.com"
        });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.NotNull(_factory.EmailSender.Code);
        var user = _factory.Users.Users.Single(u => u.Email == "integration@example.com");
        Assert.False(user.IsActive);

        using var confirmResponse = await client.PostAsJsonAsync("/api/auth/confirm", new
        {
            Email = "integration@example.com",
            Code = _factory.EmailSender.Code
        });

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.True(user.EmailConfirmed);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task Register_WhenEmailCannotBeSent_Returns503AndCreatesNoAccount()
    {
        using var client = Client();
        _factory.EmailSender.Fail = true;
        try
        {
            using var response = await client.PostAsJsonAsync("/api/auth/register", new
            {
                FullName = "Unlucky User",
                Password = "correct-horse-battery",
                Email = "unsendable@example.com"
            });

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.DoesNotContain(_factory.Users.Users, u => u.Email == "unsendable@example.com");
        }
        finally
        {
            _factory.EmailSender.Fail = false;
        }
    }
}

public sealed class SwitchableEmailSender : IEmailConfirmationSender
{
    public bool Fail { get; set; }
    public string? Code { get; private set; }

    public Task SendAsync(string email, string fullName, string code, CancellationToken cancellationToken)
    {
        if (Fail)
            throw new System.Net.Mail.SmtpException("Simulated SMTP failure");
        Code = code;
        return Task.CompletedTask;
    }
}

public sealed class NullDeveloperLogRepository : IDeveloperLogRepository
{
    public Task<long?> TryWriteAsync(DeveloperLog log) => Task.FromResult<long?>(null);
    public Task<IEnumerable<DeveloperLog>> SearchAsync(string? severity, string? search, int limit) =>
        Task.FromResult(Enumerable.Empty<DeveloperLog>());
    public Task<IEnumerable<SeverityCount>> CountsSinceAsync(DateTime since) =>
        Task.FromResult(Enumerable.Empty<SeverityCount>());
}
