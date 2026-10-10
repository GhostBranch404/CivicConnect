using CivicConnect.Core.Data;
using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services
{
    public interface IRequestService
    {
        Task<int> RequestSubmission(ServiceRequest request);
        Task<ServiceRequest?> GetRequestById(int id);
        Task<IEnumerable<ServiceRequest>> GetAllRequests();
        Task<bool> UpdateRequestStatus(int id, string newStatus);
    }

    public class RequestService : IRequestService
    {
        private readonly IRequestRepository repository;
        private static readonly HashSet<string> ValidStatus = new() { "Pending", "In Progress", "Resolved"};

        public RequestService(IRequestRepository repository)
        {
            this.repository = repository;
        }

        public async Task<int> RequestSubmission(ServiceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Description))
            {
                throw new ArgumentException("Invalid submission.Requires username and description.");
            }

            request.RequestStatus = "Pending";
            request.RequestDate = DateTime.UtcNow;
            return await repository.CreateNewRequest(request);
        }

        public async Task<ServiceRequest?>GetRequestById(int id) => await repository.GetRequestById(id);

        public async Task<IEnumerable<ServiceRequest>> GetAllRequests() => await repository.GetAllRequests();

        public async Task<bool> UpdateRequestStatus(int id, string newStatus)
        {
            if (!ValidStatus.Contains(newStatus))
            {
                throw new ArgumentException($"Invalid status value: '{newStatus}'. Allowed values: Pending, In Progress, Resolved.");
            }

            return await repository.UpdateRequestStatus(id, newStatus);
        }
    }
}