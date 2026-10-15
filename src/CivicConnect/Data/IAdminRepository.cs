using CivicConnect.Core.Models;

namespace CivicConnect.Core.Data
{
    public interface IAdminRepository
    {
        Task<User?> GetUserByIdAsync(int userId);
        Task<IEnumerable<User>> SearchUsersAsync(string? name, string? role);
        Task<IEnumerable<User>> GetApproversAsync();
        Task<bool> HasPendingForUserAsync(int targetUserId);
        Task<bool> HasPendingForEmailAsync(string email);
        Task<int> CreateApprovalAsync(AdminApproval approval);
        Task<AdminApproval?> GetApprovalAsync(int approvalId);
        Task<IEnumerable<AdminApproval>> GetPendingApprovalsAsync();

        // Applies the change to Users and marks the approval Approved, in one transaction.
        Task<AdminApproval> ApplyApprovalAsync(int approvalId, int decidedByUserId);
        Task DeclineApprovalAsync(int approvalId, int decidedByUserId);
    }
}
