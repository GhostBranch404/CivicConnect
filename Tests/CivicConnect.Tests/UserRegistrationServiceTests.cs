using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace CivicConnect.Tests;

public class UserRegistrationServiceTests
{
    private static UserRegistrationRequest NewSignUp(string email = "Test@example.com") => new()
    {
        FullName = "Test User",
        Password = "correct-horse-battery",
        Email = email
    };

    [Fact]
    public async Task RegisterAndConfirmEmail_StoresHashedPasswordAndConfirmsUser()
    {
        var repository = new FakeConfirmationRepository();
        var emailSender = new CapturingEmailSender();
        var service = CreateService(repository, emailSender);

        var userId = await service.RegisterAsync(NewSignUp(), CancellationToken.None);

        Assert.Equal(1, userId);
        var user = repository.Single();
        Assert.Equal("test@example.com", user.Email);
        Assert.NotEqual("correct-horse-battery", user.PasswordHash);
        Assert.False(user.IsActive);
        Assert.Equal("test@example.com", emailSender.Email);
        Assert.Matches(@"^\d{6}$", emailSender.Code);

        var confirmed = await service.ConfirmAsync(new ConfirmUserRequest
        {
            Email = "TEST@example.com",
            Code = emailSender.Code!
        });

        Assert.True(confirmed);
        Assert.True(user.EmailConfirmed);
        Assert.True(user.IsActive);
        Assert.Null(user.ConfirmationCodeProtected);
    }

    [Fact]
    public async Task Register_WhenEmailCannotBeSent_DoesNotCreateAccount()
    {
        var repository = new FakeConfirmationRepository();
        var service = CreateService(repository, new FailingEmailSender());

        var ex = await Assert.ThrowsAsync<ConfirmationDeliveryException>(() =>
            service.RegisterAsync(NewSignUp(), CancellationToken.None));

        Assert.IsType<System.Net.Mail.SmtpException>(ex.InnerException);
        Assert.Empty(repository.Users);
    }

    [Fact]
    public async Task Register_WhenEmailFailsForRepeatSignUp_KeepsPreviousPendingDetails()
    {
        var repository = new FakeConfirmationRepository();
        var emailSender = new CapturingEmailSender();
        await CreateService(repository, emailSender).RegisterAsync(NewSignUp(), CancellationToken.None);
        var before = repository.Single().ConfirmationCodeProtected;

        await Assert.ThrowsAsync<ConfirmationDeliveryException>(() =>
            CreateService(repository, new FailingEmailSender()).RegisterAsync(NewSignUp(), CancellationToken.None));

        Assert.Equal(before, repository.Single().ConfirmationCodeProtected);
    }

    [Fact]
    public async Task Register_AgainBeforeConfirming_ReplacesCodeInsteadOfFailing()
    {
        var repository = new FakeConfirmationRepository();
        var emailSender = new CapturingEmailSender();
        var service = CreateService(repository, emailSender);

        await service.RegisterAsync(NewSignUp(), CancellationToken.None);
        var firstCode = emailSender.Code;
        await service.RegisterAsync(NewSignUp(), CancellationToken.None);

        Assert.Single(repository.Users);
        if (firstCode != emailSender.Code)
            Assert.False(await service.ConfirmAsync(new ConfirmUserRequest { Email = "test@example.com", Code = firstCode! }));
        Assert.True(await service.ConfirmAsync(new ConfirmUserRequest { Email = "test@example.com", Code = emailSender.Code! }));
    }

    [Fact]
    public async Task Register_WithEmailOfConfirmedAccount_IsRejectedWithoutSendingEmail()
    {
        var repository = new FakeConfirmationRepository();
        var emailSender = new CapturingEmailSender();
        var service = CreateService(repository, emailSender);
        await service.RegisterAsync(NewSignUp(), CancellationToken.None);
        await service.ConfirmAsync(new ConfirmUserRequest { Email = "test@example.com", Code = emailSender.Code! });
        emailSender.Reset();

        await Assert.ThrowsAsync<AccountAlreadyExistsException>(() =>
            service.RegisterAsync(NewSignUp(), CancellationToken.None));

        Assert.Null(emailSender.Code);
        Assert.Single(repository.Users);
    }

    [Theory]
    [InlineData("", "correct-horse-battery", "a@example.com")]
    [InlineData("Test User", "short", "a@example.com")]
    [InlineData("Test User", "correct-horse-battery", "not-an-email")]
    [InlineData("Test User", "correct-horse-battery", "")]
    public async Task Register_WithInvalidDetails_ThrowsAndSavesNothing(string name, string password, string email)
    {
        var repository = new FakeConfirmationRepository();
        var emailSender = new CapturingEmailSender();
        var service = CreateService(repository, emailSender);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(new UserRegistrationRequest
        {
            FullName = name,
            Password = password,
            Email = email
        }, CancellationToken.None));

        Assert.Empty(repository.Users);
        Assert.Null(emailSender.Code);
    }

    [Fact]
    public async Task ConfirmWithIncorrectCode_DoesNotConfirmUser()
    {
        var repository = new FakeConfirmationRepository();
        var emailSender = new CapturingEmailSender();
        var service = CreateService(repository, emailSender);

        await service.RegisterAsync(NewSignUp(), CancellationToken.None);

        var confirmed = await service.ConfirmAsync(new ConfirmUserRequest
        {
            Email = "test@example.com",
            Code = emailSender.Code == "000000" ? "000001" : "000000"
        });

        Assert.False(confirmed);
        Assert.False(repository.Single().EmailConfirmed);
        Assert.False(repository.Single().IsActive);
    }

    [Fact]
    public async Task ConfirmWithRepeatedIncorrectCodes_RequiresResendingCode()
    {
        var repository = new FakeConfirmationRepository();
        var emailSender = new CapturingEmailSender();
        var service = CreateService(repository, emailSender);
        await service.RegisterAsync(NewSignUp(), CancellationToken.None);
        var wrong = emailSender.Code == "000000" ? "000001" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
            Assert.False(await service.ConfirmAsync(new ConfirmUserRequest { Email = "test@example.com", Code = wrong }));

        Assert.Equal(5, repository.Single().ConfirmationAttempts);
        Assert.False(await service.ConfirmAsync(new ConfirmUserRequest { Email = "test@example.com", Code = emailSender.Code! }));

        await service.ResendConfirmationAsync(new ResendConfirmationRequest { Email = "test@example.com" }, CancellationToken.None);
        Assert.Equal(0, repository.Single().ConfirmationAttempts);
        Assert.True(await service.ConfirmAsync(new ConfirmUserRequest { Email = "test@example.com", Code = emailSender.Code! }));
    }

    [Fact]
    public async Task Resend_ForUnknownOrConfirmedEmail_SendsNothing()
    {
        var repository = new FakeConfirmationRepository();
        var emailSender = new CapturingEmailSender();
        var service = CreateService(repository, emailSender);

        await service.ResendConfirmationAsync(new ResendConfirmationRequest { Email = "nobody@example.com" }, CancellationToken.None);
        Assert.Null(emailSender.Code);

        await service.RegisterAsync(NewSignUp(), CancellationToken.None);
        await service.ConfirmAsync(new ConfirmUserRequest { Email = "test@example.com", Code = emailSender.Code! });
        emailSender.Reset();

        await service.ResendConfirmationAsync(new ResendConfirmationRequest { Email = "test@example.com" }, CancellationToken.None);
        Assert.Null(emailSender.Code);
    }

    [Fact]
    public async Task Resend_WhenEmailCannotBeSent_Throws()
    {
        var repository = new FakeConfirmationRepository();
        await CreateService(repository, new CapturingEmailSender()).RegisterAsync(NewSignUp(), CancellationToken.None);

        await Assert.ThrowsAsync<ConfirmationDeliveryException>(() =>
            CreateService(repository, new FailingEmailSender())
                .ResendConfirmationAsync(new ResendConfirmationRequest { Email = "test@example.com" }, CancellationToken.None));
    }

    [Fact]
    public void EmailOptions_ReadFlatAndSectionKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email_Host"] = "smtp.resend.com",
                ["EmailPort"] = "587",
                ["EmailEnableSsl"] = "true",
                ["EmailUserName"] = "resend",
                ["Email:Password"] = "re_test",
                ["Email_FromAddress"] = "no-reply@example.com"
            })
            .Build();
        var options = new EmailSmtpOptions();

        EmailSmtpOptions.Bind(options, configuration);

        Assert.Equal("smtp.resend.com", options.Host);
        Assert.Equal(587, options.Port);
        Assert.True(options.EnableSsl);
        Assert.Equal("resend", options.UserName);
        Assert.Equal("re_test", options.Password);
        Assert.Equal("no-reply@example.com", options.FromAddress);
        Assert.Empty(options.MissingSettings());
    }

    [Fact]
    public async Task SmtpSender_WithoutConfiguration_FailsSoNoAccountIsCreated()
    {
        var repository = new FakeConfirmationRepository();
        var sender = new SmtpEmailConfirmationSender(Microsoft.Extensions.Options.Options.Create(new EmailSmtpOptions()));
        var service = CreateService(repository, sender);

        var ex = await Assert.ThrowsAsync<ConfirmationDeliveryException>(() =>
            service.RegisterAsync(NewSignUp(), CancellationToken.None));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Empty(repository.Users);
    }

    private static UserRegistrationService CreateService(
        IUserConfirmationRepository repository,
        IEmailConfirmationSender emailSender) =>
        new(
            repository,
            emailSender,
            new EphemeralDataProtectionProvider(),
            new PasswordHasher<CivicUser>());
}

/// <summary>In-memory stand-in for the Users table that behaves like the real transaction.</summary>
public sealed class FakeConfirmationRepository : IUserConfirmationRepository
{
    public List<CivicUser> Users { get; } = new();

    public CivicUser Single() => Assert.Single(Users);

    public async Task<int> CreatePendingUserAsync(CivicUser user, Func<Task> deliverCode)
    {
        var existing = Users.FirstOrDefault(u => u.Email == user.Email);
        if (existing is not null && !existing.IsPendingConfirmation)
            throw new AccountAlreadyExistsException();

        // Nothing is saved unless the email goes out (the real repository rolls back).
        await deliverCode();

        if (existing is null)
        {
            user.UserId = Users.Count + 1;
            Users.Add(user);
            return user.UserId;
        }

        existing.FullName = user.FullName;
        existing.PasswordHash = user.PasswordHash;
        existing.ConfirmationCodeProtected = user.ConfirmationCodeProtected;
        existing.ConfirmationCodeExpiresAt = user.ConfirmationCodeExpiresAt;
        existing.ConfirmationAttempts = 0;
        return existing.UserId;
    }

    public Task<CivicUser?> FindByEmailAsync(string email) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

    public Task UpdateConfirmationCodeAsync(int userId, string protectedCode, DateTime expiresAt)
    {
        var user = Users.Single(u => u.UserId == userId && u.IsPendingConfirmation);
        user.ConfirmationCodeProtected = protectedCode;
        user.ConfirmationCodeExpiresAt = expiresAt;
        user.ConfirmationAttempts = 0;
        return Task.CompletedTask;
    }

    public Task IncrementConfirmationAttemptsAsync(int userId, string expectedProtectedCode)
    {
        var user = Users.FirstOrDefault(u => u.UserId == userId);
        if (user is not null && user.IsPendingConfirmation &&
            user.ConfirmationCodeProtected == expectedProtectedCode && user.ConfirmationAttempts < 5)
        {
            user.ConfirmationAttempts++;
        }
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmUserAsync(int userId, string expectedProtectedCode)
    {
        var user = Users.FirstOrDefault(u => u.UserId == userId);
        if (user is null || !user.IsPendingConfirmation || user.ConfirmationCodeProtected != expectedProtectedCode)
            return Task.FromResult(false);

        user.EmailConfirmed = true;
        user.IsActive = true;
        user.ConfirmationCodeProtected = null;
        user.ConfirmationCodeExpiresAt = null;
        user.ConfirmationAttempts = 0;
        return Task.FromResult(true);
    }
}

public sealed class CapturingEmailSender : IEmailConfirmationSender
{
    public string? Email { get; private set; }
    public string? Code { get; private set; }

    public void Reset() => Email = Code = null;

    public Task SendAsync(string email, string fullName, string code, CancellationToken cancellationToken)
    {
        Email = email;
        Code = code;
        return Task.CompletedTask;
    }
}

public sealed class FailingEmailSender : IEmailConfirmationSender
{
    public Task SendAsync(string email, string fullName, string code, CancellationToken cancellationToken) =>
        throw new System.Net.Mail.SmtpException("Mailbox unavailable");
}
