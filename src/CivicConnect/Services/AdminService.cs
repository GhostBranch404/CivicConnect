using System.Text.RegularExpressions;
using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace CivicConnect.Core.Services
{
    // User management for Admins (Role 'Manager') and Devs (Role 'Dev').
    // Promote, remove access and creating an Admin always need a SECOND admin's approval.
    public class AdminService : IAdminService
    {
        private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);

        private readonly IAdminRepository _admin;
        private readonly IUserRepository _users;
        private readonly INotificationService _notify;
        private readonly PasswordHasher<User> _hasher = new();

        public AdminService(IAdminRepository admin, IUserRepository users, INotificationService notify)
        {
            _admin = admin;
            _users = users;
            _notify = notify;
        }

        public static string Label(string? role) => role switch
        {
            UserRoles.Staff => "Worker",
            UserRoles.Manager => "Admin",
            UserRoles.Dev => "Dev",
            _ => "Requester"
        };

        private static bool IsAdmin(User u) =>
            u.IsActive && (u.Role == UserRoles.Manager || u.Role == UserRoles.Dev);

        // Requester -> Worker -> Admin. Admins and Devs can't be promoted further.
        private static string? NextRole(string role) => role switch
        {
            UserRoles.Requester or "Citizen" => UserRoles.Staff,
            UserRoles.Staff => UserRoles.Manager,
            _ => null
        };

        private static string? NormaliseRole(string? role) => (role ?? "").Trim().ToLowerInvariant() switch
        {
            "" => null,
            "requester" or "citizen" => UserRoles.Requester,
            "staff" or "worker" => UserRoles.Staff,
            "manager" or "admin" => UserRoles.Manager,
            "dev" or "developer" => UserRoles.Dev,
            _ => throw new ArgumentException($"Unknown role '{role}'.")
        };

        private async Task<User> RequireAdminAsync(int actingUserId)
        {
            var actor = await _admin.GetUserByIdAsync(actingUserId);
            if (actor is null || !IsAdmin(actor))
                throw new UnauthorizedAccessException("Only admins and devs can manage users.");
            return actor;
        }

        private async Task<User> GetTargetAsync(User actor, int targetUserId)
        {
            if (targetUserId == actor.UserId)
                throw new ArgumentException("You can't change your own access.");

            var target = await _admin.GetUserByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("That user no longer exists.");

            if (!target.IsActive)
                throw new ArgumentException($"{target.FullName}'s access has already been removed.");

            if (await _admin.HasPendingForUserAsync(target.UserId))
                throw new InvalidOperationException($"There is already a change for {target.FullName} waiting for approval.");

            return target;
        }

        private async Task NotifyApproversAsync(User actor, string description)
        {
            var approvers = (await _admin.GetApproversAsync()).Where(a => a.UserId != actor.UserId);
            await _notify.SendAsync(
                approvers,
                "CivicConnect: approval needed",
                $"{actor.FullName} has requested: {description}. Sign in to CivicConnect to approve or decline it.");
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(int actingUserId, string? name, string? role)
        {
            await RequireAdminAsync(actingUserId);
            var n = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            return await _admin.SearchUsersAsync(n, NormaliseRole(role));
        }

        public async Task<AdminActionResult> RequestPromoteAsync(int actingUserId, int targetUserId)
        {
            var actor = await RequireAdminAsync(actingUserId);
            var target = await GetTargetAsync(actor, targetUserId);
            var newRole = NextRole(target.Role)
                ?? throw new ArgumentException($"{target.FullName} is already {Label(target.Role)} and can't be promoted further.");

            var approval = new AdminApproval
            {
                ActionType = AdminActions.Promote,
                TargetUserId = target.UserId,
                OldRole = target.Role,
                NewRole = newRole,
                RequestedByUserId = actor.UserId,
                RequestedAt = DateTime.UtcNow
            };
            approval.ApprovalId = await _admin.CreateApprovalAsync(approval);

            var what = $"promote {target.FullName} from {Label(target.Role)} to {Label(newRole)}";
            await NotifyApproversAsync(actor, what);
            return new AdminActionResult { Pending = true, Message = $"Waiting for another admin to approve: {what}.", Approval = approval };
        }

        public async Task<AdminActionResult> RequestRemoveAccessAsync(int actingUserId, int targetUserId)
        {
            var actor = await RequireAdminAsync(actingUserId);
            var target = await GetTargetAsync(actor, targetUserId);

            var approval = new AdminApproval
            {
                ActionType = AdminActions.RemoveAccess,
                TargetUserId = target.UserId,
                OldRole = target.Role,
                RequestedByUserId = actor.UserId,
                RequestedAt = DateTime.UtcNow
            };
            approval.ApprovalId = await _admin.CreateApprovalAsync(approval);

            var what = $"remove access for {target.FullName} ({Label(target.Role)})";
            await NotifyApproversAsync(actor, what);
            return new AdminActionResult { Pending = true, Message = $"Waiting for another admin to approve: {what}.", Approval = approval };
        }

        public async Task<AdminActionResult> CreateUserAsync(AdminCreateUserRequest request)
        {
            var actor = await RequireAdminAsync(request.ActingUserId);

            var name = request.FullName?.Trim() ?? string.Empty;
            var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var password = request.Password ?? string.Empty;
            var role = NormaliseRole(request.Role);

            if (name.Length == 0) throw new ArgumentException("Please enter a name.");
            if (!EmailPattern.IsMatch(email)) throw new ArgumentException("Please enter a valid email address.");
            if (password.Length < 8) throw new ArgumentException("Password must be at least 8 characters.");
            if (role is not (UserRoles.Staff or UserRoles.Manager or UserRoles.Dev))
                throw new ArgumentException("Choose Worker, Dev or Admin.");

            if (await _users.GetByEmailAsync(email) is not null || await _admin.HasPendingForEmailAsync(email))
                throw new InvalidOperationException("An account with this email already exists or is waiting for approval.");

            var user = new User { FullName = name, Email = email, Role = role, IsActive = true, CreatedAt = DateTime.UtcNow };
            var hash = _hasher.HashPassword(user, password);

            if (role == UserRoles.Manager)
            {
                // New admins need a second admin's approval before the account exists.
                var approval = new AdminApproval
                {
                    ActionType = AdminActions.CreateUser,
                    NewRole = role,
                    NewFullName = name,
                    NewEmail = email,
                    NewPasswordHash = hash,
                    RequestedByUserId = actor.UserId,
                    RequestedAt = DateTime.UtcNow
                };
                approval.ApprovalId = await _admin.CreateApprovalAsync(approval);

                var what = $"add {name} ({email}) as an Admin";
                await NotifyApproversAsync(actor, what);
                return new AdminActionResult { Pending = true, Message = $"Waiting for another admin to approve: {what}.", Approval = approval };
            }

            user.PasswordHash = hash;
            user.UserId = await _users.CreateAsync(user);
            user.PasswordHash = null;
            return new AdminActionResult { Pending = false, Message = $"{name} was added as a {Label(role)}.", User = user };
        }

        public async Task<IEnumerable<AdminApproval>> GetPendingApprovalsAsync(int actingUserId)
        {
            await RequireAdminAsync(actingUserId);
            return await _admin.GetPendingApprovalsAsync();
        }

        public async Task<AdminActionResult> ApproveAsync(int actingUserId, int approvalId)
        {
            var actor = await RequireAdminAsync(actingUserId);
            var pending = await _admin.GetApprovalAsync(approvalId)
                ?? throw new KeyNotFoundException("That approval request no longer exists.");

            if (pending.RequestedByUserId == actor.UserId)
                throw new InvalidOperationException("A different admin has to approve this - you can't approve your own request.");

            var done = await _admin.ApplyApprovalAsync(approvalId, actor.UserId);
            return new AdminActionResult { Pending = false, Message = "Approved and applied.", Approval = done };
        }

        public async Task<AdminActionResult> DeclineAsync(int actingUserId, int approvalId)
        {
            var actor = await RequireAdminAsync(actingUserId);
            var pending = await _admin.GetApprovalAsync(approvalId)
                ?? throw new KeyNotFoundException("That approval request no longer exists.");

            await _admin.DeclineApprovalAsync(approvalId, actor.UserId);
            var cancelled = pending.RequestedByUserId == actor.UserId;
            return new AdminActionResult { Pending = false, Message = cancelled ? "Request cancelled." : "Request declined." };
        }
    }
}
