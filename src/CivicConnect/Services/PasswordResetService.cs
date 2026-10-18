using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using CivicConnect.Core.Models;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace CivicConnect.Core.Services;

/// <summary>
/// Forgot password: emails a 6-digit code, exchanges the right code for a short-lived reset token,
/// then sets the new password. Codes are kept in memory only (10 minutes, 5 tries).
/// </summary>
public sealed class PasswordResetService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

    private sealed class CodeState
    {
        public string CodeHash = string.Empty;
        public int Attempts;
    }

    private readonly string _connectionString;
    private readonly IMemoryCache _cache;
    private readonly EmailSmtpOptions _options;
    private readonly IDataProtector _protector;
    private readonly IPasswordHasher<CivicUser> _hasher;

    public PasswordResetService(
        string connectionString,
        IMemoryCache cache,
        IOptions<EmailSmtpOptions> options,
        IDataProtectionProvider dataProtection,
        IPasswordHasher<CivicUser> hasher)
    {
        _connectionString = connectionString;
        _cache = cache;
        _options = options.Value;
        _protector = dataProtection.CreateProtector("CivicConnect.PasswordReset.v1");
        _hasher = hasher;
    }

    /// <summary>Emails a code if an active account exists. Silent otherwise.</summary>
    public async Task RequestCodeAsync(string? emailInput, CancellationToken cancellationToken)
    {
        var email = UserRegistrationService.NormalizeEmail(emailInput);
        var user = await FindActiveUserAsync(email);
        if (user is null)
            return;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        _cache.Set(Key(email), new CodeState { CodeHash = Hash(code) }, CodeLifetime);
        try
        {
            await SendAsync(email, user.FullName, code, cancellationToken);
        }
        catch
        {
            _cache.Remove(Key(email));
            throw;
        }
    }

    /// <summary>Returns a reset token if the code is right, otherwise null.</summary>
    public async Task<string?> VerifyCodeAsync(string? emailInput, string? codeInput)
    {
        var email = UserRegistrationService.NormalizeEmail(emailInput);
        var code = codeInput?.Trim() ?? string.Empty;
        if (!_cache.TryGetValue(Key(email), out CodeState? state) || state is null)
            return null;

        lock (state)
        {
            if (state.Attempts >= MaxAttempts)
                return null;
            var ok = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Hash(code)), Encoding.UTF8.GetBytes(state.CodeHash));
            if (!ok)
            {
                state.Attempts++;
                return null;
            }
            state.Attempts = MaxAttempts;
        }

        var user = await FindActiveUserAsync(email);
        if (user is null)
            return null;
        _cache.Remove(Key(email));

        var expires = DateTime.UtcNow.Add(TokenLifetime).Ticks;
        return _protector.Protect($"{email}|{expires}|{Hash(user.PasswordHash)}");
    }

    /// <exception cref="ArgumentException">Invalid password.</exception>
    public async Task<bool> ResetPasswordAsync(string? token, string? newPassword)
    {
        var password = newPassword ?? string.Empty;
        if (password.Length < UserRegistrationService.MinPasswordLength)
            throw new ArgumentException($"Password must be at least {UserRegistrationService.MinPasswordLength} characters.");
        if (password.Length > UserRegistrationService.MaxPasswordLength)
            throw new ArgumentException($"Password must be {UserRegistrationService.MaxPasswordLength} characters or fewer.");

        string[] parts;
        try
        {
            parts = _protector.Unprotect(token ?? string.Empty).Split('|');
        }
        catch (CryptographicException)
        {
            return false;
        }
        if (parts.Length != 3 || !long.TryParse(parts[1], out var ticks) || ticks < DateTime.UtcNow.Ticks)
            return false;

        var user = await FindActiveUserAsync(parts[0]);
        if (user is null || Hash(user.PasswordHash) != parts[2])
            return false;

        var newHash = _hasher.HashPassword(user, password);
        await using var connection = new MySqlConnection(_connectionString);
        var updated = await connection.ExecuteAsync(
            "UPDATE Users SET PasswordHash = @Hash WHERE UserId = @UserId AND PasswordHash = @OldHash;",
            new { Hash = newHash, user.UserId, OldHash = user.PasswordHash });
        return updated == 1;
    }

    private async Task<CivicUser?> FindActiveUserAsync(string email)
    {
        await using var connection = new MySqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<CivicUser>(
            "SELECT UserId, FullName, Email, PasswordHash FROM Users WHERE Email = @Email AND IsActive = TRUE;",
            new { Email = email });
        return user is null || string.IsNullOrEmpty(user.PasswordHash) ? null : user;
    }

    private static string Key(string email) => "pwreset:" + email;

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async Task SendAsync(string email, string fullName, string code, CancellationToken cancellationToken)
    {
        var missing = _options.MissingSettings();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Email is not configured. Missing: {string.Join(", ", missing)}.");
        if (_options.Port == 465)
            throw new InvalidOperationException("Email Port 465 is not supported. Use 587 with EnableSsl = true.");

        var from = new MailAddress(_options.FromAddress);
        if (string.IsNullOrWhiteSpace(from.DisplayName) && !string.IsNullOrWhiteSpace(_options.FromName))
            from = new MailAddress(from.Address, _options.FromName);

        var name = string.IsNullOrWhiteSpace(fullName) ? "Hi" : $"Hi {fullName.Trim().Split(' ')[0]}";
        using var message = new MailMessage(from, new MailAddress(email))
        {
            Subject = $"Your CivicConnect password reset code: {code}",
            Body = $"{name},\n\nYour CivicConnect password reset code is: {code}\n\n" +
                   "It expires in 10 minutes. If you didn't ask to reset your password, you can ignore this email.\n",
            IsBodyHtml = false
        };

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = _options.TimeoutSeconds * 1000
        };
        if (!string.IsNullOrWhiteSpace(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);

        await client.SendMailAsync(message, cancellationToken);
    }
}
