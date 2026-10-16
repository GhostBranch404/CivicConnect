using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace CivicConnect.Tests;

public class UserRegistrationServiceTests
{
    [Fact]
    public async Task RegisterAndConfirmEmail_StoresHashedPasswordAndConfirmsUser()
    {
        var repository = new FakeUserRepository();
        var emailSender = new CapturingConfirmationSender();
        var smsSender = new CapturingConfirmationSender();
        var service = CreateService(repository, emailSender, smsSender);

        var userId = await service.RegisterAsync(new UserRegistrationRequest
        {
            FullName = "Test User",
            Password = "correct-horse-battery",
            ConfirmationMethod = "Email",
            Email = "Test@example.com"
        }, CancellationToken.None);

        Assert.Equal(1, userId);
        Assert.Equal("test@example.com", repository.User!.Email);
        Assert.NotEqual("correct-horse-battery", repository.User.PasswordHash);
        Assert.Equal("test@example.com", emailSender.Destination);
        Assert.Null(smsSender.Code);

        var confirmed = await service.ConfirmAsync(new ConfirmUserRequest
        {
            ConfirmationMethod = "Email",
            Destination = "TEST@example.com",
            Code = emailSender.Code!
        });

        Assert.True(confirmed);
        Assert.True(repository.User.EmailConfirmed);
        Assert.Null(repository.User.ConfirmationCodeProtected);
    }

    [Fact]
    public async Task ConfirmWithIncorrectCode_DoesNotConfirmUser()
    {
        var repository = new FakeUserRepository();
        var emailSender = new CapturingConfirmationSender();
        var service = CreateService(repository, emailSender, new CapturingConfirmationSender());

        await service.RegisterAsync(new UserRegistrationRequest
        {
            FullName = "Test User",
            Password = "correct-horse-battery",
            ConfirmationMethod = "Email",
            Email = "test@example.com"
        }, CancellationToken.None);

        var confirmed = await service.ConfirmAsync(new ConfirmUserRequest
        {
            ConfirmationMethod = "Email",
            Destination = "test@example.com",
            Code = emailSender.Code == "000000" ? "000001" : "000000"
        });

        Assert.False(confirmed);
        Assert.False(repository.User!.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmWithRepeatedIncorrectCodes_RequiresResendingCode()
    {
        var repository = new FakeUserRepository();
        var emailSender = new CapturingConfirmationSender();
        var service = CreateService(repository, emailSender, new CapturingConfirmationSender());

        await service.RegisterAsync(new UserRegistrationRequest
        {
            FullName = "Test User",
            Password = "correct-horse-battery",
            ConfirmationMethod = "Email",
            Email = "test@example.com"
        }, CancellationToken.None);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var confirmed = await service.ConfirmAsync(new ConfirmUserRequest
            {
                ConfirmationMethod = "Email",
                Destination = "test@example.com",
                Code = emailSender.Code == "000000" ? "000001" : "000000"
            });

            Assert.False(confirmed);
        }

        Assert.Equal(5, repository.User!.ConfirmationAttempts);
        Assert.False(await service.ConfirmAsync(new ConfirmUserRequest
        {
            ConfirmationMethod = "Email",
            Destination = "test@example.com",
            Code = emailSender.Code!
        }));
    }

    [Fact]
    public async Task RegisterSms_SendsCodeToE164PhoneNumber()
    {
        var repository = new FakeUserRepository();
        var smsSender = new CapturingConfirmationSender();
        var service = CreateService(repository, new CapturingConfirmationSender(), smsSender);

        await service.RegisterAsync(new UserRegistrationRequest
        {
            FullName = "Test User",
            Password = "correct-horse-battery",
            ConfirmationMethod = "Sms",
            PhoneNumber = "+15551234567"
        }, CancellationToken.None);

        Assert.Equal("+15551234567", smsSender.Destination);
        Assert.Matches(@"^\d{6}$", smsSender.Code);
        Assert.Null(repository.User!.Email);
        Assert.Equal("+15551234567", repository.User.PhoneNumber);
    }

    private static UserRegistrationService CreateService(
        FakeUserRepository repository,
        CapturingConfirmationSender emailSender,
        CapturingConfirmationSender smsSender) =>
        new(
            repository,
            emailSender,
            smsSender,
            new EphemeralDataProtectionProvider(),
            new PasswordHasher<CivicUser>());

    private sealed class FakeUserRepository : IUserConfirmationRepository
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

    private sealed class CapturingConfirmationSender : IEmailConfirmationSender, ISmsConfirmationSender
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
}
