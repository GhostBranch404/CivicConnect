using System.Net;
using System.Net.Http.Json;
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
    public FakeUserRepository Users { get; } = new();
    public CapturingConfirmationSender EmailSender { get; } = new();
    public CapturingConfirmationSender SmsSender { get; } = new();

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
            services.RemoveAll<ISmsConfirmationSender>();
            services.AddSingleton<ISmsConfirmationSender>(SmsSender);
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

    [Fact]
    public async Task RegisterAndConfirmEmail_ReturnsCreatedThenSuccess()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = "Integration User",
            Password = "correct-horse-battery",
            ConfirmationMethod = "Email",
            Email = "integration@example.com"
        });

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.NotNull(_factory.EmailSender.Code);
        Assert.False(_factory.Users.User!.IsActive);

        using var confirmResponse = await client.PostAsJsonAsync("/api/auth/confirm", new
        {
            ConfirmationMethod = "Email",
            Destination = "integration@example.com",
            Code = _factory.EmailSender.Code
        });

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.True(_factory.Users.User.EmailConfirmed);
        Assert.True(_factory.Users.User.IsActive);
    }
}

public sealed class FakeUserRepository : IUserConfirmationRepository
{
    public CivicUser? User { get; private set; }

    public Task<int> CreatePendingUserAsync(CivicUser user)
    {
        user.UserId = 1;
        User = user;
        return Task.FromResult(user.UserId);
    }

    public Task<CivicUser?> FindByConfirmationDestinationAsync(string method, string destination)
    {
        var matches = method == "Email"
            ? User?.Email == destination
            : User?.PhoneNumber == destination;
        return Task.FromResult(matches ? User : null);
    }

    public Task UpdateConfirmationCodeAsync(int userId, string protectedCode, DateTime expiresAt)
    {
        User!.ConfirmationCodeProtected = protectedCode;
        User.ConfirmationCodeExpiresAt = expiresAt;
        User.ConfirmationAttempts = 0;
        return Task.CompletedTask;
    }

    public Task IncrementConfirmationAttemptsAsync(int userId, string expectedProtectedCode)
    {
        if (User is not null && User.ConfirmationCodeProtected == expectedProtectedCode)
            User.ConfirmationAttempts++;
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmUserAsync(int userId, string method, string expectedProtectedCode)
    {
        if (User is null || User.UserId != userId ||
            User.ConfirmationCodeProtected != expectedProtectedCode)
        {
            return Task.FromResult(false);
        }

        if (method == "Email")
            User.EmailConfirmed = true;
        else
            User.PhoneNumberConfirmed = true;
        User.IsActive = true;
        User.ConfirmationCodeProtected = null;
        User.ConfirmationCodeExpiresAt = null;
        return Task.FromResult(true);
    }
}

public sealed class CapturingConfirmationSender : IEmailConfirmationSender, ISmsConfirmationSender
{
    public string? Destination { get; private set; }
    public string? Code { get; private set; }

    public Task SendAsync(string destination, string code, CancellationToken cancellationToken)
    {
        Destination = destination;
        Code = code;
        return Task.CompletedTask;
    }
}
