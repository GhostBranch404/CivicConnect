using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services
{
    public interface IRequestService
    {
        Task<int> RequestSubmission(ServiceRequest request);
        Task<ServiceRequest?> GetRequestById(int id);
        Task<IEnumerable<ServiceRequest>> GetAllRequests();

        Task<bool> UpdateRequestStatus(
            int id,
            string newStatus,
            int changedByUserId,
            int expectedVersion,
            string? notes = null);
    }
}
