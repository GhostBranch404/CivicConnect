using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services
{
    public interface IAuditHistoryService
    {
        Task RecordChangeAsync(RequestHistory history);
    }
}
