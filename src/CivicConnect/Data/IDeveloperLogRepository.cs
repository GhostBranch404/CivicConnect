using CivicConnect.Core.Models;

namespace CivicConnect.Core.Data
{
    public interface IDeveloperLogRepository
    {
        // Never throws: logging must not break the request that is being logged.
        Task<long?> TryWriteAsync(DeveloperLog log);
        Task<IEnumerable<DeveloperLog>> SearchAsync(string? severity, string? search, int limit);
        Task<IEnumerable<SeverityCount>> CountsSinceAsync(DateTime since);
    }

    public class SeverityCount
    {
        public string Severity { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
