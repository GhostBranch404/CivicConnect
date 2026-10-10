namespace CivicConnect.Core.Models
{
    public class ServiceRequest
    {
        public int RequestId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RequestStatus { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    }
}