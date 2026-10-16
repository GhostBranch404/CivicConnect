using CivicConnect.Core.Models;

namespace CivicConnect.Core.Data
{
    public interface IRequestDetailsRepository
    {
        Task<IEnumerable<Category>> GetCategoriesAsync();
        Task<IEnumerable<RequestStatusItem>> GetStatusesAsync();
        Task<IEnumerable<RequestHistoryEntry>> GetHistoryAsync(int requestId);
        Task<IEnumerable<RequestComment>> GetCommentsAsync(int requestId);
        Task<int> AddCommentAsync(int requestId, int userId, string text);
        Task<IEnumerable<AssignmentInfo>> GetCurrentAssignmentsAsync();
        Task<bool> AssignAsync(int requestId, int? staffUserId, int changedByUserId);
    }
}
