namespace CivicConnect.Core.Models;

/// <summary>A row of the Users table as used by sign-up and email confirmation.</summary>
public sealed class CivicUser
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Requester";
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? ConfirmationCodeProtected { get; set; }
    public DateTime? ConfirmationCodeExpiresAt { get; set; }
    public int ConfirmationAttempts { get; set; }

    /// <summary>
    /// A self sign-up that hasn't been confirmed yet. Accounts added by an admin have no
    /// confirmation code, and deactivated accounts were confirmed, so neither counts.
    /// </summary>
    public bool IsPendingConfirmation =>
        !IsActive && !EmailConfirmed && ConfirmationCodeProtected is not null;
}
