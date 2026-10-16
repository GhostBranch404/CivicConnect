using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services
{
    public interface IAdminService
    {
        Task<IEnumerable<User>> SearchUsersAsync(int actingUserId, string? name, string? role);
        Task<AdminActionResult> RequestPromoteAsync(int actingUserId, int targetUserId);
        Task<AdminActionResult> RequestRemoveAccessAsync(int actingUserId, int targetUserId);
        Task<AdminActionResult> CreateUserAsync(AdminCreateUserRequest request);
        Task<IEnumerable<AdminApproval>> GetPendingApprovalsAsync(int actingUserId);
        Task<AdminActionResult> ApproveAsync(int actingUserId, int approvalId);
        Task<AdminActionResult> DeclineAsync(int actingUserId, int approvalId);
    }
}
