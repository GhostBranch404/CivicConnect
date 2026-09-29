namespace CivicConnect.Core.Models
{
    public class DeveloperLog
    {
        public long LogId { get; set; }
        public int? RequestId { get; set; }
        public string Actor { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string Severity { get; set; } = "Information";
        public string? Details { get; set; }
        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    }
}
