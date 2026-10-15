using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Dapper;
using MySqlConnector;

namespace CivicConnect.Core.Data;

public sealed class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> CreatePendingUserAsync(CivicUser user)
    {
        await using var connection = new MySqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO Users
                (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive,
                 EmailConfirmed, PhoneNumberConfirmed, ConfirmationMethod,
                 ConfirmationCodeProtected, ConfirmationCodeExpiresAt, ConfirmationAttempts, CreatedAt)
            VALUES
                (@FullName, @Email, @PhoneNumber, @PasswordHash, @Role, FALSE,
                 FALSE, FALSE, @ConfirmationMethod,
                 @ConfirmationCodeProtected, @ConfirmationCodeExpiresAt, 0, UTC_TIMESTAMP());
            SELECT LAST_INSERT_ID();";

        return await connection.ExecuteScalarAsync<int>(sql, user);
    }

    public async Task<CivicUser?> FindByConfirmationDestinationAsync(string method, string destination)
    {
        await using var connection = new MySqlConnection(_connectionString);
        var contactColumn = method switch
        {
            "Email" => "Email",
            "Sms" => "PhoneNumber",
            _ => throw new ArgumentException("Unsupported confirmation method.", nameof(method))
        };
        var sql = $@"
            SELECT UserId, FullName, Email, PhoneNumber, PasswordHash, Role,
                   EmailConfirmed, PhoneNumberConfirmed, ConfirmationMethod,
                   ConfirmationCodeProtected, ConfirmationCodeExpiresAt, ConfirmationAttempts
            FROM Users
            WHERE {contactColumn} = @Destination;";

        return await connection.QuerySingleOrDefaultAsync<CivicUser>(sql, new { Destination = destination });
    }

    public async Task UpdateConfirmationCodeAsync(int userId, string protectedCode, DateTime expiresAt)
    {
        await using var connection = new MySqlConnection(_connectionString);
        const string sql = @"
            UPDATE Users
            SET ConfirmationCodeProtected = @ProtectedCode,
                ConfirmationCodeExpiresAt = @ExpiresAt,
                ConfirmationAttempts = 0
            WHERE UserId = @UserId
              AND EmailConfirmed = FALSE
              AND PhoneNumberConfirmed = FALSE;";

        var updated = await connection.ExecuteAsync(
            sql,
            new { UserId = userId, ProtectedCode = protectedCode, ExpiresAt = expiresAt });
        if (updated != 1)
            throw new InvalidOperationException("The confirmation code could not be refreshed.");
    }

    public async Task IncrementConfirmationAttemptsAsync(int userId, string expectedProtectedCode)
    {
        await using var connection = new MySqlConnection(_connectionString);
        const string sql = @"
            UPDATE Users
            SET ConfirmationAttempts = ConfirmationAttempts + 1
            WHERE UserId = @UserId
              AND ConfirmationCodeProtected = @ExpectedProtectedCode
              AND ConfirmationAttempts < 5
              AND EmailConfirmed = FALSE
              AND PhoneNumberConfirmed = FALSE;";

        await connection.ExecuteAsync(
            sql,
            new { UserId = userId, ExpectedProtectedCode = expectedProtectedCode });
    }

    public async Task<bool> ConfirmUserAsync(int userId, string method, string expectedProtectedCode)
    {
        await using var connection = new MySqlConnection(_connectionString);
        var (confirmedColumn, contactColumn) = method switch
        {
            "Email" => ("EmailConfirmed", "Email"),
            "Sms" => ("PhoneNumberConfirmed", "PhoneNumber"),
            _ => throw new ArgumentException("Unsupported confirmation method.", nameof(method))
        };
        var sql = $@"
            UPDATE Users
            SET {confirmedColumn} = TRUE,
                IsActive = TRUE,
                ConfirmationCodeProtected = NULL,
                ConfirmationCodeExpiresAt = NULL,
                ConfirmationAttempts = 0
            WHERE UserId = @UserId
              AND {confirmedColumn} = FALSE
              AND ConfirmationCodeProtected = @ExpectedProtectedCode
              AND ConfirmationAttempts < 5
              AND ConfirmationCodeExpiresAt > UTC_TIMESTAMP()
              AND {contactColumn} IS NOT NULL;";

        return await connection.ExecuteAsync(
            sql,
            new { UserId = userId, ExpectedProtectedCode = expectedProtectedCode }) == 1;
    }
}
