using System.Text.Json.Serialization;

namespace CivicConnect.Core.Models
{
    public static class UserRoles
    {
        public const string Requester = "Requester";
        public const string Staff = "Staff";
        public const string Manager = "Manager"; // shown as "Admin"
        public const string Dev = "Dev";
    }

    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = UserRoles.Requester;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Never sent to the browser.
        [JsonIgnore]
        public string? PasswordHash { get; set; }
    }

    public class RegisterRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
