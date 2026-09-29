using CivicConnect.Core.Models;

namespace CivicConnect.Core.Data
{
    public interface IRequestRepository
    {
        Task<int> CreateNewRequest(ServiceRequest request);
        Task<ServiceRequest?> GetRequestById(int id);
        Task<IEnumerable<ServiceRequest>> GetAllRequests();

        Task<bool> UpdateRequestStatus(
            int id,
            string status,
            int changedByUserId,
            int expectedVersion,
            string? notes = null);
    }
}
