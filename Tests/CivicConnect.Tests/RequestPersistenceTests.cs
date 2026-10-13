using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using Dapper;
using MySqlConnector;

namespace CivicConnect.Tests;

public class RequestPersistenceTests
{
    private const int PendingStatusId = 1;
    private const int InProgressStatusId = 2;

    [DatabaseFact]
    public async Task CreateNewRequest_PersistsRequestHistoryAndDeveloperLogs()
    {
        var (userId, categoryId) = await DatabaseTestHelper.CreateUserAndCategoryAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var request = DatabaseTestHelper.NewRequest(userId, categoryId);

            var requestId = await repository.CreateNewRequest(request);

            var saved = await repository.GetRequestById(requestId);
            Assert.NotNull(saved);
            Assert.Equal(request.TicketNumber, saved.TicketNumber);
            Assert.Equal("Persistence test request", saved.Title);
            Assert.Equal(1, saved.Version);
            Assert.Equal("Pending", saved.RequestStatus);

            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM RequestHistory WHERE RequestId = @Id AND ActionType = 'RequestCreated';",
                new { Id = requestId }));
            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM DeveloperLogs WHERE RequestId = @Id AND Action = 'CREATE';",
                new { Id = requestId }));
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(userId, categoryId);
        }
    }

    [DatabaseFact]
    public async Task CreateNewRequest_WithUnknownRequester()
    {
        var (userId, categoryId) = await DatabaseTestHelper.CreateUserAndCategoryAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var request = DatabaseTestHelper.NewRequest(int.MaxValue, categoryId);

            await Assert.ThrowsAsync<MySqlException>(() => repository.CreateNewRequest(request));

            Assert.Equal(0, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM ServiceRequests WHERE TicketNumber = @Ticket;",
                new { Ticket = request.TicketNumber }));
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(userId, categoryId);
        }
    }

    [DatabaseFact]
    public async Task UpdateRequestStatus_IncrementsVersionAndLogs()
    {
        var (userId, categoryId) = await DatabaseTestHelper.CreateUserAndCategoryAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var requestId = await repository.CreateNewRequest(DatabaseTestHelper.NewRequest(userId, categoryId));

            var updated = await repository.UpdateRequestStatus(requestId, "In Progress", userId, 1);

            Assert.True(updated);
            var saved = await repository.GetRequestById(requestId);
            Assert.NotNull(saved);
            Assert.Equal(InProgressStatusId, saved.StatusId);
            Assert.Equal(2, saved.Version);
            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM DeveloperLogs WHERE RequestId = @Id AND Action = 'UPDATE_STATUS';",
                new { Id = requestId }));
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(userId, categoryId);
        }
    }

    [DatabaseFact]
    public async Task UpdateRequestStatus_WithStaleVersion()
    {
        var (userId, categoryId) = await DatabaseTestHelper.CreateUserAndCategoryAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var requestId = await repository.CreateNewRequest(DatabaseTestHelper.NewRequest(userId, categoryId));
            await repository.UpdateRequestStatus(requestId, "In Progress", userId, 1);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.UpdateRequestStatus(requestId, "Resolved", userId, 1, "Stale update"));

            var saved = await repository.GetRequestById(requestId);
            Assert.NotNull(saved);
            Assert.Equal(InProgressStatusId, saved.StatusId);
            Assert.Equal(2, saved.Version);
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(userId, categoryId);
        }
    }

    [DatabaseFact]
    public async Task GetAllRequests_ReturnsPersistedRequest()
    {
        var (userId, categoryId) = await DatabaseTestHelper.CreateUserAndCategoryAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var requestId = await repository.CreateNewRequest(DatabaseTestHelper.NewRequest(userId, categoryId));

            var requests = (await repository.GetAllRequests()).ToList();

            var saved = Assert.Single(requests, r => r.RequestId == requestId);
            Assert.Equal("Persistence test request", saved.Title);
            Assert.Equal(userId, saved.RequesterId);
            Assert.Equal(categoryId, saved.CategoryId);
            Assert.Equal("Pending", saved.RequestStatus);
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(userId, categoryId);
        }
    }

    [DatabaseFact]
    public async Task UpdateRequestStatus_WithUnknownStatus()
    {
        var (userId, categoryId) = await DatabaseTestHelper.CreateUserAndCategoryAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var requestId = await repository.CreateNewRequest(DatabaseTestHelper.NewRequest(userId, categoryId));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                repository.UpdateRequestStatus(requestId, "No Such Status", userId, 1));

            var saved = await repository.GetRequestById(requestId);
            Assert.NotNull(saved);
            Assert.Equal(PendingStatusId, saved.StatusId);
            Assert.Equal(1, saved.Version);
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(userId, categoryId);
        }
    }

    [DatabaseFact]
    public async Task RecordRequestHistory_PersistsHistoryRow()
    {
        var (userId, categoryId) = await DatabaseTestHelper.CreateUserAndCategoryAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var requestId = await repository.CreateNewRequest(DatabaseTestHelper.NewRequest(userId, categoryId));

            await repository.RecordRequestHistoryAsync(new RequestHistory
            {
                RequestId = requestId,
                ChangedByUserId = userId,
                OldStatusId = PendingStatusId,
                NewStatusId = InProgressStatusId,
                ActionType = "StatusChanged",
                Notes = "Persistence test history",
                ChangedAt = DateTime.UtcNow
            });

            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                """
                SELECT COUNT(*) FROM RequestHistory
                WHERE RequestId = @Id AND ActionType = 'StatusChanged'
                  AND OldStatusId = @OldStatusId AND NewStatusId = @NewStatusId
                  AND Notes = 'Persistence test history';
                """,
                new { Id = requestId, OldStatusId = PendingStatusId, NewStatusId = InProgressStatusId }));
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(userId, categoryId);
        }
    }
}

internal static class DatabaseTestHelper
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("CIVICCONNECT_TEST_CONNECTION")
        ?? throw new InvalidOperationException(
            "Set the CIVICCONNECT_TEST_CONNECTION environment variable to a test MySQL connection string before running database tests.");

    public static async Task<(int UserId, int CategoryId)> CreateUserAndCategoryAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            "INSERT INTO Users (FullName, Email, Role) VALUES (@FullName, @Email, 'Citizen');",
            new { FullName = $"Test User {suffix}", Email = $"test-{suffix}@example.com" });
        var userId = await connection.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID();");

        await connection.ExecuteAsync(
            "INSERT INTO Categories (CategoryName) VALUES (@Name);",
            new { Name = $"Test Category {suffix}" });
        var categoryId = await connection.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID();");

        return (userId, categoryId);
    }

    public static ServiceRequest NewRequest(int requesterId, int categoryId) => new()
    {
        RequesterId = requesterId,
        CategoryId = categoryId,
        StatusId = 1,
        Title = "Persistence test request",
        Description = "Created by a database persistence test.",
        Location = "Test location"
    };

    public static async Task<int> CountAsync(string sql, object parameters)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(sql, parameters);
    }

    public static async Task CleanupAsync(int userId, int categoryId)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        var parameters = new { UserId = userId, CategoryId = categoryId };

        await connection.ExecuteAsync(
            "DELETE FROM DeveloperLogs WHERE RequestId IN (SELECT RequestId FROM ServiceRequests WHERE RequesterId = @UserId);",
            parameters);
        await connection.ExecuteAsync(
            "DELETE FROM RequestHistory WHERE RequestId IN (SELECT RequestId FROM ServiceRequests WHERE RequesterId = @UserId);",
            parameters);
        await connection.ExecuteAsync("DELETE FROM ServiceRequests WHERE RequesterId = @UserId;", parameters);
        await connection.ExecuteAsync("DELETE FROM Users WHERE UserId = @UserId;", parameters);
        await connection.ExecuteAsync("DELETE FROM Categories WHERE CategoryId = @CategoryId;", parameters);
    }
}

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("CIVICCONNECT_TEST_CONNECTION")))
        {
            Skip = "Set CIVICCONNECT_TEST_CONNECTION to run MySQL integration tests.";
        }

    }
}
