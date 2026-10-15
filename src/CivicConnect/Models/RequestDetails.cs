namespace CivicConnect.Core.Models
{
    public class Category
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class RequestStatusItem
    {
        public int StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
    }

    public class RequestHistoryEntry
    {
        public int HistoryId { get; set; }
        public int RequestId { get; set; }
        public int ChangedByUserId { get; set; }
        public string? ChangedByName { get; set; }
        public string? OldStatus { get; set; }
        public string? NewStatus { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class RequestComment
    {
        public int CommentId { get; set; }
        public int RequestId { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string CommentText { get; set; } = string.Empty;
        public string CommentType { get; set; } = "Public";
        public DateTime CreatedAt { get; set; }
    }

    public class NewCommentRequest
    {
        public int UserId { get; set; }
        public string CommentText { get; set; } = string.Empty;
    }

    public class AssignmentInfo
    {
        public int AssignmentId { get; set; }
        public int RequestId { get; set; }
        public int StaffUserId { get; set; }
        public string? StaffName { get; set; }
        public DateTime AssignedAt { get; set; }
    }

    public class AssignRequest
    {
        // null = unassign
        public int? StaffUserId { get; set; }
        public int ChangedByUserId { get; set; }
    }
}
