namespace CivicConnect.Core.Models
{
    public class ServiceRequest
    {
        public int RequestId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;

        public int RequesterId { get; set; }
        public int CategoryId { get; set; }
        public int StatusId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int Version { get; set; } = 1;

        // Optional display values populated by joined read queries.
        public string? RequesterName { get; set; }
        public string? CategoryName { get; set; }
        public string? RequestStatus { get; set; }
    }
}
