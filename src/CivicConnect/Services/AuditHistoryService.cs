using CivicConnect.Core.Data;
using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services
{
    public class AuditHistoryService : IAuditHistoryService
    {
        private readonly IRequestRepository _repository;

        public AuditHistoryService(IRequestRepository repository)
        {
            _repository = repository;
        }

        public async Task RecordChangeAsync(RequestHistory history)
        {
            ArgumentNullException.ThrowIfNull(history);

            if (history.RequestId <= 0)
                throw new ArgumentException("A valid request identifier is required.", nameof(history));

            if (history.ChangedByUserId <= 0)
                throw new ArgumentException("A valid acting user is required.", nameof(history));

            if (history.ActionType.Length == 0)
                throw new ArgumentException("An action type is required.", nameof(history));

            await _repository.RecordRequestHistoryAsync(history);
        }
    }
}
