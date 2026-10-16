using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CivicConnect.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace CivicConnect.Core.Services;

public sealed class UserRegistrationService
{
    private static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromMinutes(10);
    private static readonly Regex E164PhoneNumber = new(@"^\+[1-9]\d{7,14}$", RegexOptions.Compiled);

    private readonly IUserConfirmationRepository _users;
    private readonly IEmailConfirmationSender _emailSender;
    private readonly ISmsConfirmationSender _smsSender;
    private readonly IDataProtector _protector;
    private readonly IPasswordHasher<CivicUser> _passwordHasher;

    public UserRegistrationService(
        IUserConfirmationRepository users,
        IEmailConfirmationSender emailSender,
        ISmsConfirmationSender smsSender,
        IDataProtectionProvider dataProtectionProvider,
        IPasswordHasher<CivicUser> passwordHasher)
    {
        _users = users;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _protector = dataProtectionProvider.CreateProtector("CivicConnect.UserConfirmation.v1");
        _passwordHasher = passwordHasher;
    }

    public async Task<int> RegisterAsync(UserRegistrationRequest request, CancellationToken cancellationToken)
    {
        var method = NormalizeMethod(request.ConfirmationMethod);
        var destination = NormalizeDestination(method, method == "Email" ? request.Email : request.PhoneNumber);
        var user = new CivicUser
        {
            FullName = request.FullName.Trim(),
            Email = method == "Email" ? destination : null,
            PhoneNumber = method == "Sms" ? destination : null,
            ConfirmationMethod = method
        };
        if (string.IsNullOrWhiteSpace(user.FullName))
            throw new ArgumentException("FullName is required.");

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        var code = CreateCode();
        user.ConfirmationCodeProtected = _protector.Protect(code);
        user.ConfirmationCodeExpiresAt = DateTime.UtcNow.Add(ConfirmationLifetime);

        var userId = await _users.CreatePendingUserAsync(user);
        await SendConfirmationAsync(method, destination, code, cancellationToken);
        return userId;
    }

    public async Task<bool> ConfirmAsync(ConfirmUserRequest request)
    {
        var method = NormalizeMethod(request.ConfirmationMethod);
        var destination = NormalizeDestination(method, request.Destination);
        var user = await _users.FindByConfirmationDestinationAsync(method, destination);

        if (user is null || IsConfirmed(user, method) ||
            user.ConfirmationCodeProtected is null ||
            user.ConfirmationCodeExpiresAt is null ||
            user.ConfirmationCodeExpiresAt <= DateTime.UtcNow ||
            user.ConfirmationAttempts >= 5)
        {
            return false;
        }

        string expectedCode;
        try
        {
            expectedCode = _protector.Unprotect(user.ConfirmationCodeProtected);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(request.Code);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedCode);
        if (suppliedBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes))
        {
            await _users.IncrementConfirmationAttemptsAsync(user.UserId, user.ConfirmationCodeProtected);
            return false;
        }

        return await _users.ConfirmUserAsync(user.UserId, method, user.ConfirmationCodeProtected);
    }

    public async Task ResendConfirmationAsync(
        ResendConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var method = NormalizeMethod(request.ConfirmationMethod);
        var destination = NormalizeDestination(method, request.Destination);
        var user = await _users.FindByConfirmationDestinationAsync(method, destination);

        if (user is null || IsConfirmed(user, method))
        {
            return;
        }

        var code = CreateCode();
        var protectedCode = _protector.Protect(code);
        await _users.UpdateConfirmationCodeAsync(
            user.UserId,
            protectedCode,
            DateTime.UtcNow.Add(ConfirmationLifetime));
        await SendConfirmationAsync(method, destination, code, cancellationToken);
    }

    private async Task SendConfirmationAsync(
        string method,
        string destination,
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            IConfirmationSender sender = method == "Email" ? _emailSender : _smsSender;
            await sender.SendAsync(destination, code, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or SmtpException)
        {
            throw new ConfirmationDeliveryException(ex);
        }
    }

    private static string NormalizeMethod(string method)
    {
        if (string.Equals(method, "Email", StringComparison.OrdinalIgnoreCase))
            return "Email";
        if (string.Equals(method, "Sms", StringComparison.OrdinalIgnoreCase))
            return "Sms";

        throw new ArgumentException("ConfirmationMethod must be 'Email' or 'Sms'.");
    }

    private static string NormalizeDestination(string method, string? destination)
    {
        var normalized = destination?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"A {method} destination is required.");

        if (method == "Email")
        {
            normalized = normalized.ToLowerInvariant();
            if (normalized.Length > 255 || !new EmailAddressAttribute().IsValid(normalized))
                throw new ArgumentException("A valid email address is required.");
        }
        else if (!E164PhoneNumber.IsMatch(normalized))
        {
            throw new ArgumentException("Phone numbers must use E.164 format, such as +15551234567.");
        }

        return normalized;
    }

    private static bool IsConfirmed(CivicUser user, string method) =>
        method == "Email" ? user.EmailConfirmed : user.PhoneNumberConfirmed;

    private static string CreateCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
}
