using CivicConnect.Core.Data;
using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services
{
    public class RequestService : IRequestService
    {
        private readonly IRequestRepository _repository;
        private static readonly HashSet<string> ValidStatuses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Pending",
                "In Progress",
                "Resolved",
                "Rejected"
            };

        public RequestService(IRequestRepository repository)
        {
            _repository = repository;
        }

        // Validates the user input and initiates request submission
        public async Task<int> RequestSubmission(ServiceRequest request)
        {
            if (request.RequesterId <= 0)
                throw new ArgumentException("A valid requester is required.");

            if (request.CategoryId <= 0)
                throw new ArgumentException("A valid category is required.");

            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("A title is required.");

            if (string.IsNullOrWhiteSpace(request.Description))
                throw new ArgumentException("A description is required.");

            if (string.IsNullOrWhiteSpace(request.Location))
                throw new ArgumentException("A location is required.");

            request.StatusId = 1; // Pending - seeded by the initial schema.
            request.RequestDate = DateTime.UtcNow;
            request.UpdatedAt = request.RequestDate;
            request.Version = 1;

            return await _repository.CreateNewRequest(request);
        }

        public Task<ServiceRequest?> GetRequestById(int id) =>
            _repository.GetRequestById(id);

        public Task<IEnumerable<ServiceRequest>> GetAllRequests() =>
            _repository.GetAllRequests();

        public async Task<bool> UpdateRequestStatus(
            int id,
            string newStatus,
            int changedByUserId,
            int expectedVersion,
            string? notes = null)
        {
            if (!ValidStatuses.Contains(newStatus))
            {
                throw new ArgumentException(
                    $"Invalid status value: '{newStatus}'. Allowed values: Pending, In Progress, Resolved, Rejected.");
            }

            if (changedByUserId <= 0)
                throw new ArgumentException("A valid acting user is required.");

            if (expectedVersion <= 0)
                throw new ArgumentException("A valid request version is required.");

            if (newStatus.Equals("Resolved", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(notes))
            {
                throw new ArgumentException("Resolution notes are required when resolving a request.");
            }

            if (newStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(notes))
            {
                throw new ArgumentException("A reason is required when rejecting a request.");
            }
            return await _repository.UpdateRequestStatus(
                id,
                newStatus,
                changedByUserId,
                expectedVersion,
                notes);
        }
    }
}
