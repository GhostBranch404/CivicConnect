namespace CivicConnect.Core.Models
{
    public class UpdateStatusRequest
    {
        public string NewStatus { get; set; } = string.Empty;
        public int ChangedByUserId { get; set; }
        public int ExpectedVersion { get; set; }
        public string? Notes { get; set; }
    }
}
