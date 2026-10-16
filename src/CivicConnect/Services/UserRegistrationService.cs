using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CivicConnect.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace CivicConnect.Core.Services;

/// <summary>
/// Self sign-up with email confirmation:
///  1. Register: the account is saved unconfirmed and a 6-digit code is emailed. If the email
///     can't be sent, nothing is saved.
///  2. Confirm: the right code (within 10 minutes, at most 5 tries) activates the account.
///  3. Resend: a fresh code for an unconfirmed account.
/// </summary>
public sealed class UserRegistrationService
{
    public const int MinPasswordLength = 10;
    public const int MaxPasswordLength = 128;
    public const int MaxAttempts = 5;
    private static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromMinutes(10);

    private readonly IUserConfirmationRepository _users;
    private readonly IEmailConfirmationSender _emailSender;
    private readonly IDataProtector _protector;
    private readonly IPasswordHasher<CivicUser> _passwordHasher;

    public UserRegistrationService(
        IUserConfirmationRepository users,
        IEmailConfirmationSender emailSender,
        IDataProtectionProvider dataProtectionProvider,
        IPasswordHasher<CivicUser> passwordHasher)
    {
        _users = users;
        _emailSender = emailSender;
        _protector = dataProtectionProvider.CreateProtector("CivicConnect.UserConfirmation.v1");
        _passwordHasher = passwordHasher;
    }

    /// <exception cref="ArgumentException">Invalid name, email or password.</exception>
    /// <exception cref="AccountAlreadyExistsException">The email already belongs to an account.</exception>
    /// <exception cref="ConfirmationDeliveryException">The email couldn't be sent; no account was saved.</exception>
    public async Task<int> RegisterAsync(UserRegistrationRequest request, CancellationToken cancellationToken)
    {
        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (fullName.Length == 0)
            throw new ArgumentException("Please enter your full name.");
        if (fullName.Length > 150)
            throw new ArgumentException("Your name must be 150 characters or fewer.");

        var email = NormalizeEmail(request.Email);

        var password = request.Password ?? string.Empty;
        if (password.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");
        if (password.Length > MaxPasswordLength)
            throw new ArgumentException($"Password must be {MaxPasswordLength} characters or fewer.");

        var user = new CivicUser { FullName = fullName, Email = email, Role = "Requester" };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        var code = CreateCode();
        user.ConfirmationCodeProtected = _protector.Protect(code);
        user.ConfirmationCodeExpiresAt = DateTime.UtcNow.Add(ConfirmationLifetime);

        // The account row is only committed once the email has been accepted by the mail server.
        return await _users.CreatePendingUserAsync(
            user,
            () => SendCodeAsync(email, fullName, code, cancellationToken));
    }

    public async Task<bool> ConfirmAsync(ConfirmUserRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var code = request.Code?.Trim() ?? string.Empty;
        var user = await _users.FindByEmailAsync(email);

        if (user is null || !user.IsPendingConfirmation ||
            user.ConfirmationCodeExpiresAt is null ||
            user.ConfirmationCodeExpiresAt <= DateTime.UtcNow ||
            user.ConfirmationAttempts >= MaxAttempts)
        {
            return false;
        }

        string expectedCode;
        try
        {
            expectedCode = _protector.Unprotect(user.ConfirmationCodeProtected!);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(code);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedCode);
        if (suppliedBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes))
        {
            await _users.IncrementConfirmationAttemptsAsync(user.UserId, user.ConfirmationCodeProtected!);
            return false;
        }

        return await _users.ConfirmUserAsync(user.UserId, user.ConfirmationCodeProtected!);
    }

    /// <summary>
    /// Emails a new code if the address has an unconfirmed sign-up. Does nothing otherwise, so the
    /// response never reveals whether an account exists.
    /// </summary>
    /// <exception cref="ConfirmationDeliveryException">The email couldn't be sent.</exception>
    public async Task ResendConfirmationAsync(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _users.FindByEmailAsync(email);
        if (user is null || !user.IsPendingConfirmation)
            return;

        var code = CreateCode();
        await _users.UpdateConfirmationCodeAsync(
            user.UserId,
            _protector.Protect(code),
            DateTime.UtcNow.Add(ConfirmationLifetime));
        await SendCodeAsync(email, user.FullName, code, cancellationToken);
    }

    private async Task SendCodeAsync(string email, string fullName, string code, CancellationToken cancellationToken)
    {
        try
        {
            await _emailSender.SendAsync(email, fullName, code, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ConfirmationDeliveryException)
        {
            throw new ConfirmationDeliveryException(ex);
        }
    }

    internal static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException("Please enter your email address.");
        if (normalized.Length > 255 || !new EmailAddressAttribute().IsValid(normalized) ||
            normalized.IndexOf('.', normalized.IndexOf('@')) < 0)
        {
            throw new ArgumentException("Please enter a valid email address.");
        }
        return normalized;
    }

    private static string CreateCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}
