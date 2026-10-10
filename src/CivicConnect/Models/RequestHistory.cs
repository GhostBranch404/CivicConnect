namespace CivicConnect.Core.Models
{
    public class RequestHistory
    {
        public int HistoryId { get; set; }
        public int RequestId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public int ChangedByUserId { get; set; }
        public int? OldStatusId { get; set; }
        public int? NewStatusId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}
