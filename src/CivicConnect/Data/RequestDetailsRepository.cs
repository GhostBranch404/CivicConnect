using CivicConnect.Core.Models;
using Dapper;
using MySqlConnector;

namespace CivicConnect.Core.Data
{
    // Reads/writes the supporting tables around a request:
    // Categories, RequestStatuses, RequestHistory, RequestComments and Assignments.
    public class RequestDetailsRepository : IRequestDetailsRepository
    {
        private readonly string _connectionString;

        public RequestDetailsRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<Category>> GetCategoriesAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            return await connection.QueryAsync<Category>(@"
                SELECT CategoryId, CategoryName
                FROM Categories
                WHERE IsActive = TRUE
                ORDER BY CategoryId;");
        }

        public async Task<IEnumerable<RequestStatusItem>> GetStatusesAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            return await connection.QueryAsync<RequestStatusItem>(@"
                SELECT StatusId, StatusName
                FROM RequestStatuses
                ORDER BY StatusId;");
        }

        public async Task<IEnumerable<RequestHistoryEntry>> GetHistoryAsync(int requestId)
        {
            await using var connection = new MySqlConnection(_connectionString);
            const string sql = @"
                SELECT
                    h.HistoryId,
                    h.RequestId,
                    h.ChangedByUserId,
                    u.FullName AS ChangedByName,
                    os.StatusName AS OldStatus,
                    ns.StatusName AS NewStatus,
                    h.ActionType,
                    h.Notes,
                    h.ChangedAt
                FROM RequestHistory h
                LEFT JOIN Users u ON u.UserId = h.ChangedByUserId
                LEFT JOIN RequestStatuses os ON os.StatusId = h.OldStatusId
                LEFT JOIN RequestStatuses ns ON ns.StatusId = h.NewStatusId
                WHERE h.RequestId = @RequestId
                ORDER BY h.ChangedAt DESC, h.HistoryId DESC;";

            return await connection.QueryAsync<RequestHistoryEntry>(sql, new { RequestId = requestId });
        }

        public async Task<IEnumerable<RequestComment>> GetCommentsAsync(int requestId)
        {
            await using var connection = new MySqlConnection(_connectionString);
            const string sql = @"
                SELECT
                    c.CommentId,
                    c.RequestId,
                    c.UserId,
                    u.FullName AS UserName,
                    c.CommentText,
                    c.CommentType,
                    c.CreatedAt
                FROM RequestComments c
                LEFT JOIN Users u ON u.UserId = c.UserId
                WHERE c.RequestId = @RequestId
                ORDER BY c.CreatedAt, c.CommentId;";

            return await connection.QueryAsync<RequestComment>(sql, new { RequestId = requestId });
        }

        public async Task<int> AddCommentAsync(int requestId, int userId, string text)
        {
            await using var connection = new MySqlConnection(_connectionString);
            const string sql = @"
                INSERT INTO RequestComments (RequestId, UserId, CommentText, CommentType, CreatedAt)
                VALUES (@RequestId, @UserId, @CommentText, 'Public', @CreatedAt);
                SELECT LAST_INSERT_ID();";

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                RequestId = requestId,
                UserId = userId,
                CommentText = text,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<IEnumerable<AssignmentInfo>> GetCurrentAssignmentsAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            const string sql = @"
                SELECT a.AssignmentId, a.RequestId, a.StaffUserId, u.FullName AS StaffName, a.AssignedAt
                FROM Assignments a
                INNER JOIN Users u ON u.UserId = a.StaffUserId
                WHERE a.IsCurrent = TRUE;";

            return await connection.QueryAsync<AssignmentInfo>(sql);
        }

        public async Task<bool> AssignAsync(int requestId, int? staffUserId, int changedByUserId)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var exists = await connection.QuerySingleOrDefaultAsync<int?>(
                    "SELECT RequestId FROM ServiceRequests WHERE RequestId = @Id FOR UPDATE;",
                    new { Id = requestId },
                    transaction);

                if (exists is null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                string? staffName = null;
                if (staffUserId is not null)
                {
                    staffName = await connection.QuerySingleOrDefaultAsync<string?>(
                        "SELECT FullName FROM Users WHERE UserId = @Id AND IsActive = TRUE AND Role IN ('Staff', 'Manager');",
                        new { Id = staffUserId },
                        transaction);

                    if (staffName is null)
                        throw new ArgumentException("The selected staff member does not exist.");
                }

                var now = DateTime.UtcNow;

                await connection.ExecuteAsync(@"
                    UPDATE Assignments
                    SET IsCurrent = FALSE, UnassignedAt = @Now
                    WHERE RequestId = @RequestId AND IsCurrent = TRUE;",
                    new { RequestId = requestId, Now = now },
                    transaction);

                if (staffUserId is not null)
                {
                    await connection.ExecuteAsync(@"
                        INSERT INTO Assignments (RequestId, StaffUserId, AssignedAt, IsCurrent)
                        VALUES (@RequestId, @StaffUserId, @Now, TRUE);",
                        new { RequestId = requestId, StaffUserId = staffUserId, Now = now },
                        transaction);
                }

                await connection.ExecuteAsync(@"
                    INSERT INTO RequestHistory
                        (RequestId, ChangedByUserId, OldStatusId, NewStatusId, ActionType, Notes, ChangedAt)
                    VALUES
                        (@RequestId, @ChangedByUserId, NULL, NULL, @ActionType, @Notes, @Now);",
                    new
                    {
                        RequestId = requestId,
                        ChangedByUserId = changedByUserId,
                        ActionType = staffUserId is null ? "Unassigned" : "Assigned",
                        Notes = staffUserId is null ? "Request unassigned." : $"Assigned to {staffName}.",
                        Now = now
                    },
                    transaction);

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
