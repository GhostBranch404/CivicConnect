using System.Text.Json.Serialization;

namespace CivicConnect.Core.Models
{
    public static class AdminActions
    {
        public const string Promote = "Promote";
        public const string RemoveAccess = "RemoveAccess";
        public const string CreateUser = "CreateUser";
    }

    public static class ApprovalStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Declined = "Declined";
    }

    public class ActingUserRequest
    {
        public int ActingUserId { get; set; }
    }

    public class AdminCreateUserRequest
    {
        public int ActingUserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // Staff (Worker) | Manager (Admin) | Dev
    }

    public class AdminApproval
    {
        public int ApprovalId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public int? TargetUserId { get; set; }
        public string? TargetName { get; set; }
        public string? OldRole { get; set; }
        public string? NewRole { get; set; }
        public string? NewFullName { get; set; }
        public string? NewEmail { get; set; }
        public int RequestedByUserId { get; set; }
        public string? RequestedByName { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Status { get; set; } = ApprovalStatuses.Pending;
        public int? DecidedByUserId { get; set; }
        public DateTime? DecidedAt { get; set; }

        [JsonIgnore]
        public string? NewPasswordHash { get; set; }
    }

    public class AdminActionResult
    {
        // true = waiting for a second admin; false = applied immediately.
        public bool Pending { get; set; }
        public string Message { get; set; } = string.Empty;
        public AdminApproval? Approval { get; set; }
        public User? User { get; set; }
    }
}
