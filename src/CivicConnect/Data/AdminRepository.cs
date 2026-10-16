using CivicConnect.Core.Models;
using Dapper;
using MySqlConnector;

namespace CivicConnect.Core.Data
{
    public class AdminRepository : IAdminRepository
    {
        private const string ApprovalSelect = @"
            SELECT
                a.ApprovalId, a.ActionType, a.TargetUserId, t.FullName AS TargetName,
                a.OldRole, a.NewRole, a.NewFullName, a.NewEmail, a.NewPasswordHash,
                a.RequestedByUserId, r.FullName AS RequestedByName, a.RequestedAt,
                a.Status, a.DecidedByUserId, a.DecidedAt
            FROM AdminApprovals a
            LEFT JOIN Users t ON t.UserId = a.TargetUserId
            LEFT JOIN Users r ON r.UserId = a.RequestedByUserId";

        private readonly string _connectionString;

        public AdminRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            await using var connection = new MySqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<User>(@"
                SELECT UserId, FullName, Email, Role, IsActive, CreatedAt
                FROM Users WHERE UserId = @UserId;", new { UserId = userId });
        }

        public async Task<IEnumerable<User>> SearchUsersAsync(string? name, string? role)
        {
            await using var connection = new MySqlConnection(_connectionString);
            const string sql = @"
                SELECT UserId, FullName, Email, Role, IsActive, CreatedAt
                FROM Users
                WHERE (@Name IS NULL OR FullName LIKE CONCAT('%', @Name, '%'))
                  AND (@Role IS NULL OR Role = @Role OR (@Role = 'Requester' AND Role = 'Citizen'))
                ORDER BY IsActive DESC, FullName;";
            return await connection.QueryAsync<User>(sql, new { Name = name, Role = role });
        }

        public async Task<IEnumerable<User>> GetApproversAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            return await connection.QueryAsync<User>(@"
                SELECT UserId, FullName, Email, Role, IsActive, CreatedAt
                FROM Users
                WHERE IsActive = TRUE AND Role IN ('Manager', 'Dev');");
        }

        public async Task<bool> HasPendingForUserAsync(int targetUserId)
        {
            await using var connection = new MySqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM AdminApprovals
                WHERE TargetUserId = @Id AND Status = 'Pending';", new { Id = targetUserId }) > 0;
        }

        public async Task<bool> HasPendingForEmailAsync(string email)
        {
            await using var connection = new MySqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM AdminApprovals
                WHERE NewEmail = @Email AND Status = 'Pending';", new { Email = email }) > 0;
        }

        public async Task<int> CreateApprovalAsync(AdminApproval approval)
        {
            await using var connection = new MySqlConnection(_connectionString);
            const string sql = @"
                INSERT INTO AdminApprovals
                    (ActionType, TargetUserId, OldRole, NewRole, NewFullName, NewEmail, NewPasswordHash,
                     RequestedByUserId, RequestedAt, Status)
                VALUES
                    (@ActionType, @TargetUserId, @OldRole, @NewRole, @NewFullName, @NewEmail, @NewPasswordHash,
                     @RequestedByUserId, @RequestedAt, 'Pending');
                SELECT LAST_INSERT_ID();";
            return await connection.ExecuteScalarAsync<int>(sql, approval);
        }

        public async Task<AdminApproval?> GetApprovalAsync(int approvalId)
        {
            await using var connection = new MySqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<AdminApproval>(
                ApprovalSelect + " WHERE a.ApprovalId = @Id;", new { Id = approvalId });
        }

        public async Task<IEnumerable<AdminApproval>> GetPendingApprovalsAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            return await connection.QueryAsync<AdminApproval>(
                ApprovalSelect + " WHERE a.Status = 'Pending' ORDER BY a.RequestedAt;");
        }

        public async Task<AdminApproval> ApplyApprovalAsync(int approvalId, int decidedByUserId)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var a = await connection.QuerySingleOrDefaultAsync<AdminApproval>(
                    "SELECT * FROM AdminApprovals WHERE ApprovalId = @Id FOR UPDATE;",
                    new { Id = approvalId }, transaction)
                    ?? throw new KeyNotFoundException("That approval request no longer exists.");

                if (a.Status != ApprovalStatuses.Pending)
                    throw new InvalidOperationException("This request has already been decided by another admin.");

                var now = DateTime.UtcNow;
                int? targetId = a.TargetUserId;

                switch (a.ActionType)
                {
                    case AdminActions.Promote:
                    {
                        var rows = await connection.ExecuteAsync(@"
                            UPDATE Users SET Role = @NewRole
                            WHERE UserId = @Id AND Role = @OldRole AND IsActive = TRUE;",
                            new { a.NewRole, a.OldRole, Id = a.TargetUserId }, transaction);
                        if (rows != 1)
                            throw new InvalidOperationException(
                                "This user's role or access changed after the request was made, so it can't be applied. Decline it and request again.");
                        break;
                    }
                    case AdminActions.RemoveAccess:
                    {
                        var rows = await connection.ExecuteAsync(@"
                            UPDATE Users SET IsActive = FALSE
                            WHERE UserId = @Id AND IsActive = TRUE;",
                            new { Id = a.TargetUserId }, transaction);
                        if (rows != 1)
                            throw new InvalidOperationException("This user's access has already been removed.");
                        break;
                    }
                    case AdminActions.CreateUser:
                    {
                        targetId = await connection.ExecuteScalarAsync<int>(@"
                            INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt)
                            VALUES (@FullName, @Email, @PasswordHash, @Role, TRUE, @Now);
                            SELECT LAST_INSERT_ID();",
                            new { FullName = a.NewFullName, Email = a.NewEmail, PasswordHash = a.NewPasswordHash, Role = a.NewRole, Now = now },
                            transaction);
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"Unknown action '{a.ActionType}'.");
                }

                await connection.ExecuteAsync(@"
                    UPDATE AdminApprovals
                    SET Status = 'Approved', DecidedByUserId = @By, DecidedAt = @Now,
                        TargetUserId = @TargetId, NewPasswordHash = NULL
                    WHERE ApprovalId = @Id;",
                    new { By = decidedByUserId, Now = now, TargetId = targetId, Id = approvalId },
                    transaction);

                await transaction.CommitAsync();

                a.Status = ApprovalStatuses.Approved;
                a.DecidedByUserId = decidedByUserId;
                a.DecidedAt = now;
                a.TargetUserId = targetId;
                a.NewPasswordHash = null;
                return a;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeclineApprovalAsync(int approvalId, int decidedByUserId)
        {
            await using var connection = new MySqlConnection(_connectionString);
            var rows = await connection.ExecuteAsync(@"
                UPDATE AdminApprovals
                SET Status = 'Declined', DecidedByUserId = @By, DecidedAt = @Now, NewPasswordHash = NULL
                WHERE ApprovalId = @Id AND Status = 'Pending';",
                new { By = decidedByUserId, Now = DateTime.UtcNow, Id = approvalId });
            if (rows != 1)
                throw new InvalidOperationException("This request has already been decided by another admin.");
        }
    }
}
