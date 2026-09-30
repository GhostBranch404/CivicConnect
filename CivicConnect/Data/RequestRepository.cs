using CivicConnect.Core.Models;
using Dapper;
using MySqlConnector;

namespace CivicConnect.Core.Data
{
// Allows for data access for the system through the service requests using Dapper and MySQL
    public class RequestRepository : IRequestRepository
    {
        private readonly string _connectionString;

        public RequestRepository(string connectionString)
        {
            _connectionString = connectionString;
        }
// Inserts a new service request into the database.
        public async Task<int> CreateNewRequest(ServiceRequest request)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                request.TicketNumber = string.IsNullOrWhiteSpace(request.TicketNumber)
                    ? $"CC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
                    : request.TicketNumber;

                request.RequestDate = DateTime.UtcNow;
                request.UpdatedAt = request.RequestDate;
                request.Version = 1;

                const string insertRequestSql = @"
                    INSERT INTO ServiceRequests
                        (TicketNumber, RequesterId, CategoryId, StatusId, Title, Description, Location, RequestDate, UpdatedAt, Version)
                    VALUES
                        (@TicketNumber, @RequesterId, @CategoryId, @StatusId, @Title, @Description, @Location, @RequestDate, @UpdatedAt, @Version);
                    SELECT LAST_INSERT_ID();";

                var requestId = await connection.ExecuteScalarAsync<int>(
                    insertRequestSql,
                    request,
                    transaction);

                const string historySql = @"
                    INSERT INTO RequestHistory
                        (RequestId, ChangedByUserId, OldStatusId, NewStatusId, ActionType, Notes, ChangedAt)
                    VALUES
                        (@RequestId, @ChangedByUserId, NULL, @NewStatusId, 'RequestCreated', @Notes, @ChangedAt);";

                await connection.ExecuteAsync(
                    historySql,
                    new
                    {
                        RequestId = requestId,
                        ChangedByUserId = request.RequesterId,
                        NewStatusId = request.StatusId,
                        Notes = "Service request created.",
                        ChangedAt = request.RequestDate
                    },
                    transaction);

                const string developerLogSql = @"
                    INSERT INTO DeveloperLogs
                        (RequestId, Actor, Action, EntityType, EntityId, Severity, Details, LoggedAt)
                    VALUES
                        (@RequestId, @Actor, 'CREATE', 'ServiceRequest', @EntityId, 'Information', @Details, @LoggedAt);";

                await connection.ExecuteAsync(
                    developerLogSql,
                    new
                    {
                        RequestId = requestId,
                        Actor = $"User:{request.RequesterId}",
                        EntityId = requestId.ToString(),
                        Details = $"Created service request {request.TicketNumber}",
                        LoggedAt = request.RequestDate
                    },
                    transaction);

                await transaction.CommitAsync();
                return requestId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

// Return the service request by its unique request id.
        public async Task<ServiceRequest?> GetRequestById(int id)
        {
            await using var connection = new MySqlConnection(_connectionString);

            const string sql = @"
                SELECT
                    sr.RequestId,
                    sr.TicketNumber,
                    sr.RequesterId,
                    sr.CategoryId,
                    sr.StatusId,
                    sr.Title,
                    sr.Description,
                    sr.Location,
                    sr.RequestDate,
                    sr.UpdatedAt,
                    sr.Version,
                    u.FullName AS RequesterName,
                    c.CategoryName,
                    rs.StatusName AS RequestStatus
                FROM ServiceRequests sr
                INNER JOIN Users u ON u.UserId = sr.RequesterId
                INNER JOIN Categories c ON c.CategoryId = sr.CategoryId
                INNER JOIN RequestStatuses rs ON rs.StatusId = sr.StatusId
                WHERE sr.RequestId = @Id;";

            return await connection.QuerySingleOrDefaultAsync<ServiceRequest>(sql, new { Id = id });
        }

        public async Task<IEnumerable<ServiceRequest>> GetAllRequests()
        {
            await using var connection = new MySqlConnection(_connectionString);

            const string sql = @"
                SELECT
                    sr.RequestId,
                    sr.TicketNumber,
                    sr.RequesterId,
                    sr.CategoryId,
                    sr.StatusId,
                    sr.Title,
                    sr.Description,
                    sr.Location,
                    sr.RequestDate,
                    sr.UpdatedAt,
                    sr.Version,
                    u.FullName AS RequesterName,
                    c.CategoryName,
                    rs.StatusName AS RequestStatus
                FROM ServiceRequests sr
                INNER JOIN Users u ON u.UserId = sr.RequesterId
                INNER JOIN Categories c ON c.CategoryId = sr.CategoryId
                INNER JOIN RequestStatuses rs ON rs.StatusId = sr.StatusId
                ORDER BY sr.RequestDate DESC;";

            return await connection.QueryAsync<ServiceRequest>(sql);
        }

        public async Task<bool> UpdateRequestStatus(
            int id,
            string status,
            int changedByUserId,
            int expectedVersion,
            string? notes = null)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                const string currentSql = @"
                    SELECT StatusId, Version
                    FROM ServiceRequests
                    WHERE RequestId = @Id
                    FOR UPDATE;";

                var current = await connection.QuerySingleOrDefaultAsync<(int StatusId, int Version)>(
                    currentSql,
                    new { Id = id },
                    transaction);

                if (current == default)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                if (current.Version != expectedVersion)
                {
                    throw new InvalidOperationException(
                        "This request was changed by another user. Reload it and try again.");
                }

                const string statusSql = @"
                    SELECT StatusId
                    FROM RequestStatuses
                    WHERE StatusName = @StatusName;";

                var newStatusId = await connection.QuerySingleOrDefaultAsync<int?>(
                    statusSql,
                    new { StatusName = status },
                    transaction);

                if (newStatusId is null)
                {
                    throw new ArgumentException($"Unknown request status: '{status}'.");
                }

                const string updateSql = @"
                    UPDATE ServiceRequests
                    SET StatusId = @NewStatusId,
                        UpdatedAt = @UpdatedAt,
                        Version = Version + 1
                    WHERE RequestId = @Id
                      AND Version = @ExpectedVersion;";

                var updatedRows = await connection.ExecuteAsync(
                    updateSql,
                    new
                    {
                        Id = id,
                        NewStatusId = newStatusId.Value,
                        UpdatedAt = DateTime.UtcNow,
                        ExpectedVersion = expectedVersion
                    },
                    transaction);

                if (updatedRows != 1)
                {
                    throw new InvalidOperationException(
                        "The request could not be updated because its version changed.");
                }

                var changedAt = DateTime.UtcNow;

                const string historySql = @"
                    INSERT INTO RequestHistory
                        (RequestId, ChangedByUserId, OldStatusId, NewStatusId, ActionType, Notes, ChangedAt)
                    VALUES
                        (@RequestId, @ChangedByUserId, @OldStatusId, @NewStatusId, 'StatusChanged', @Notes, @ChangedAt);";

                await connection.ExecuteAsync(
                    historySql,
                    new
                    {
                        RequestId = id,
                        ChangedByUserId = changedByUserId,
                        OldStatusId = current.StatusId,
                        NewStatusId = newStatusId.Value,
                        Notes = notes,
                        ChangedAt = changedAt
                    },
                    transaction);

                const string logSql = @"
                    INSERT INTO DeveloperLogs
                        (RequestId, Actor, Action, EntityType, EntityId, Severity, Details, LoggedAt)
                    VALUES
                        (@RequestId, @Actor, 'UPDATE_STATUS', 'ServiceRequest', @EntityId, 'Information', @Details, @LoggedAt);";

                await connection.ExecuteAsync(
                    logSql,
                    new
                    {
                        RequestId = id,
                        Actor = $"User:{changedByUserId}",
                        EntityId = id.ToString(),
                        Details = $"Status changed from {current.StatusId} to {newStatusId.Value}",
                        LoggedAt = changedAt
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
