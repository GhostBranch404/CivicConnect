using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using Dapper;
using MySqlConnector;

namespace CivicConnect.Tests;

public class RequestPersistenceTests
{
    [Fact]
    public async Task ValidRequest_SavesAndRetrievesWithInitialStateHistoryAndDeveloperLog()
    {
        var scope = await DatabaseTestHelper.CreateScopeAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var request = DatabaseTestHelper.NewRequest(scope.UserId, scope.CategoryId);

            var requestId = await repository.CreateNewRequest(request);

            var saved = await repository.GetRequestById(requestId);
            Assert.NotNull(saved);
            Assert.Equal(request.TicketNumber, saved.TicketNumber);
            Assert.Equal(scope.UserId, saved.RequesterId);
            Assert.Equal("Pending", saved.RequestStatus);
            Assert.Equal(1, saved.Version);

            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM RequestHistory WHERE RequestId = @RequestId AND ActionType = 'RequestCreated';",
                new { RequestId = requestId }));
            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM DeveloperLogs WHERE RequestId = @RequestId AND Action = 'CREATE';",
                new { RequestId = requestId }));
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(scope);
        }
    }

    [Fact]
    public async Task InvalidRequester_FailsWithoutPartialRecords()
    {
        var scope = await DatabaseTestHelper.CreateScopeAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var request = DatabaseTestHelper.NewRequest(int.MaxValue, scope.CategoryId);

            await Assert.ThrowsAsync<MySqlException>(() => repository.CreateNewRequest(request));

            Assert.Equal(0, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM ServiceRequests WHERE TicketNumber = @TicketNumber;",
                new { request.TicketNumber }));
            Assert.Equal(0, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM RequestHistory WHERE RequestId IN (SELECT RequestId FROM ServiceRequests WHERE TicketNumber = @TicketNumber);",
                new { request.TicketNumber }));
            Assert.Equal(0, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM DeveloperLogs WHERE RequestId IN (SELECT RequestId FROM ServiceRequests WHERE TicketNumber = @TicketNumber);",
                new { request.TicketNumber }));
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(scope);
        }
    }

    [Fact]
    public async Task DuplicateTicket_IsRejectedWithoutPartialRecords()
    {
        var scope = await DatabaseTestHelper.CreateScopeAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var duplicateTicket = $"CC-DUP-{scope.Suffix[..8].ToUpperInvariant()}";

            var original = DatabaseTestHelper.NewRequest(scope.UserId, scope.CategoryId, duplicateTicket);
            var duplicate = DatabaseTestHelper.NewRequest(scope.UserId, scope.CategoryId, duplicateTicket);

            var originalId = await repository.CreateNewRequest(original);
            await Assert.ThrowsAsync<MySqlException>(() => repository.CreateNewRequest(duplicate));

            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM ServiceRequests WHERE TicketNumber = @TicketNumber;",
                new { TicketNumber = duplicateTicket }));
            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM RequestHistory WHERE RequestId = @RequestId AND ActionType = 'RequestCreated';",
                new { RequestId = originalId }));
            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM DeveloperLogs WHERE RequestId = @RequestId AND Action = 'CREATE';",
                new { RequestId = originalId }));
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(scope);
        }
    }

    [Fact]
    public async Task ResolveRequest_SavesStatusVersionNotesHistoryAndDeveloperLog()
    {
        var scope = await DatabaseTestHelper.CreateScopeAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var requestId = await repository.CreateNewRequest(DatabaseTestHelper.NewRequest(scope.UserId, scope.CategoryId));
            const string resolutionNotes = "Issue fixed and verified by staff.";

            var updated = await repository.UpdateRequestStatus(requestId, "Resolved", scope.UserId, 1, resolutionNotes);

            Assert.True(updated);
            var saved = await repository.GetRequestById(requestId);
            Assert.NotNull(saved);
            Assert.Equal(3, saved.StatusId);
            Assert.Equal("Resolved", saved.RequestStatus);
            Assert.Equal(2, saved.Version);

            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                @"SELECT COUNT(*) FROM RequestHistory
                  WHERE RequestId = @RequestId
                    AND ActionType = 'StatusChanged'
                    AND NewStatusId = 3
                    AND Notes = @Notes;",
                new { RequestId = requestId, Notes = resolutionNotes }));
            Assert.Equal(1, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM DeveloperLogs WHERE RequestId = @RequestId AND Action = 'UPDATE_STATUS';",
                new { RequestId = requestId }));
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(scope);
        }
    }

    [Fact]
    public async Task StatusUpdate_RollsBackWhenRelatedWriteFailsAfterTransactionStarts()
    {
        var scope = await DatabaseTestHelper.CreateScopeAsync();
        try
        {
            var repository = new RequestRepository(DatabaseTestHelper.ConnectionString);
            var requestId = await repository.CreateNewRequest(DatabaseTestHelper.NewRequest(scope.UserId, scope.CategoryId));

            await Assert.ThrowsAsync<MySqlException>(() =>
                repository.UpdateRequestStatus(requestId, "In Progress", int.MaxValue, 1, "Force failure"));

            var saved = await repository.GetRequestById(requestId);
            Assert.NotNull(saved);
            Assert.Equal(1, saved.StatusId);
            Assert.Equal("Pending", saved.RequestStatus);
            Assert.Equal(1, saved.Version);

            Assert.Equal(0, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM RequestHistory WHERE RequestId = @RequestId AND ActionType = 'StatusChanged';",
                new { RequestId = requestId }));
            Assert.Equal(0, await DatabaseTestHelper.CountAsync(
                "SELECT COUNT(*) FROM DeveloperLogs WHERE RequestId = @RequestId AND Action = 'UPDATE_STATUS';",
                new { RequestId = requestId }));
        }
        finally
        {
            await DatabaseTestHelper.CleanupAsync(scope);
        }
    }
}

internal static class DatabaseTestHelper
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("CIVICCONNECT_TEST_CONNECTION")
        ?? throw new InvalidOperationException(
            "Set CIVICCONNECT_TEST_CONNECTION to an isolated MySQL test database connection string.");

    public static async Task<TestScope> CreateScopeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            "INSERT INTO Users (FullName, Email, Role) VALUES (@FullName, @Email, 'Citizen');",
            new { FullName = $"DB Test User {suffix[..8]}", Email = $"dbtest-{suffix}@example.com" });
        var userId = await connection.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID();");

        await connection.ExecuteAsync(
            "INSERT INTO Categories (CategoryName) VALUES (@CategoryName);",
            new { CategoryName = $"DB Test Category {suffix[..8]}" });
        var categoryId = await connection.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID();");

        return new TestScope(userId, categoryId, suffix);
    }

    public static ServiceRequest NewRequest(int requesterId, int categoryId, string? ticketNumber = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return new ServiceRequest
        {
            RequesterId = requesterId,
            CategoryId = categoryId,
            StatusId = 1,
            TicketNumber = ticketNumber ?? $"CC-DB-{suffix}",
            Title = $"Persistence Request {suffix}",
            Description = "Created by database persistence verification.",
            Location = "Test location"
        };
    }

    public static async Task<int> CountAsync(string sql, object parameters)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<int>(sql, parameters);
    }

    public static async Task CleanupAsync(TestScope scope)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            "DELETE FROM DeveloperLogs WHERE RequestId IN (SELECT RequestId FROM ServiceRequests WHERE RequesterId = @UserId);",
            new { scope.UserId });
        await connection.ExecuteAsync(
            "DELETE FROM RequestHistory WHERE RequestId IN (SELECT RequestId FROM ServiceRequests WHERE RequesterId = @UserId);",
            new { scope.UserId });
        await connection.ExecuteAsync("DELETE FROM ServiceRequests WHERE RequesterId = @UserId;", new { scope.UserId });
        await connection.ExecuteAsync("DELETE FROM Users WHERE UserId = @UserId;", new { scope.UserId });
        await connection.ExecuteAsync("DELETE FROM Categories WHERE CategoryId = @CategoryId;", new { scope.CategoryId });
    }

    internal readonly record struct TestScope(int UserId, int CategoryId, string Suffix);
}