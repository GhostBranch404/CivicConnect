namespace CivicConnect.Core.Models;

public sealed class CivicUser
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Requester";
    public bool IsActive { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public string ConfirmationMethod { get; set; } = string.Empty;
    public string? ConfirmationCodeProtected { get; set; }
    public DateTime? ConfirmationCodeExpiresAt { get; set; }
    public int ConfirmationAttempts { get; set; }
}
