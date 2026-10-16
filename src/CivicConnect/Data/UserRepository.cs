using CivicConnect.Core.Models;
using CivicConnect.Core.Services;
using Dapper;
using MySqlConnector;

namespace CivicConnect.Core.Data;

public sealed class UserRepository : IUserRepository, IUserConfirmationRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        await using var connection = new MySqlConnection(_connectionString);
        const string sql = @"
            SELECT UserId, FullName, Email, Role, IsActive, CreatedAt, PasswordHash
            FROM Users
            WHERE Email = @Email;";

        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task<IEnumerable<User>> GetAllAsync(string? role = null)
    {
        await using var connection = new MySqlConnection(_connectionString);
        const string sql = @"
            SELECT UserId, FullName, Email, Role, IsActive, CreatedAt
            FROM Users
            WHERE (@Role IS NULL OR Role = @Role)
            ORDER BY FullName;";

        return await connection.QueryAsync<User>(sql, new { Role = role });
    }

    public async Task<int> CreateAsync(User user)
    {
        await using var connection = new MySqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO Users (FullName, Email, PasswordHash, Role, IsActive, CreatedAt)
            VALUES (@FullName, @Email, @PasswordHash, @Role, @IsActive, @CreatedAt);
            SELECT LAST_INSERT_ID();";

        return await connection.ExecuteScalarAsync<int>(sql, user);
    }

    // ---------- Email confirmation (sign-up) ----------
    // Only Email, EmailConfirmed and the ConfirmationCode* columns are used. PhoneNumber,
    // PhoneNumberConfirmed and ConfirmationMethod are not touched, so they can be dropped.

    private const string ConfirmationColumns = @"
        UserId, FullName, Email, PasswordHash, Role, IsActive, EmailConfirmed,
        ConfirmationCodeProtected, ConfirmationCodeExpiresAt, ConfirmationAttempts";

    // An unconfirmed self sign-up: never activated, never confirmed, and has a code.
    private const string IsPendingSql =
        "IsActive = FALSE AND EmailConfirmed = FALSE AND ConfirmationCodeProtected IS NOT NULL";

    public async Task<int> CreatePendingUserAsync(CivicUser user, Func<Task> deliverCode)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            // Locks the row (or the gap where it would go) so two sign-ups for one email can't race.
            var existing = await connection.QuerySingleOrDefaultAsync<CivicUser>(
                $"SELECT {ConfirmationColumns} FROM Users WHERE Email = @Email FOR UPDATE;",
                new { user.Email },
                transaction);

            int userId;
            if (existing is null)
            {
                userId = await connection.ExecuteScalarAsync<int>(@"
                    INSERT INTO Users
                        (FullName, Email, PasswordHash, Role, IsActive, EmailConfirmed,
                         ConfirmationCodeProtected, ConfirmationCodeExpiresAt, ConfirmationAttempts, CreatedAt)
                    VALUES
                        (@FullName, @Email, @PasswordHash, @Role, FALSE, FALSE,
                         @ConfirmationCodeProtected, @ConfirmationCodeExpiresAt, 0, UTC_TIMESTAMP());
                    SELECT LAST_INSERT_ID();",
                    user,
                    transaction);
            }
            else if (existing.IsPendingConfirmation)
            {
                // Signing up again before confirming: replace the old details and code.
                userId = existing.UserId;
                await connection.ExecuteAsync($@"
                    UPDATE Users
                    SET FullName = @FullName,
                        PasswordHash = @PasswordHash,
                        ConfirmationCodeProtected = @ConfirmationCodeProtected,
                        ConfirmationCodeExpiresAt = @ConfirmationCodeExpiresAt,
                        ConfirmationAttempts = 0
                    WHERE UserId = @UserId AND {IsPendingSql};",
                    new
                    {
                        UserId = userId,
                        user.FullName,
                        user.PasswordHash,
                        user.ConfirmationCodeProtected,
                        user.ConfirmationCodeExpiresAt
                    },
                    transaction);
            }
            else
            {
                throw new AccountAlreadyExistsException();
            }

            // Send the email while the transaction is still open. If it fails, roll back so no
            // account is left behind.
            await deliverCode();

            await transaction.CommitAsync();
            user.UserId = userId;
            return userId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<CivicUser?> FindByEmailAsync(string email)
    {
        await using var connection = new MySqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<CivicUser>(
            $"SELECT {ConfirmationColumns} FROM Users WHERE Email = @Email;",
            new { Email = email });
    }

    public async Task UpdateConfirmationCodeAsync(int userId, string protectedCode, DateTime expiresAt)
    {
        await using var connection = new MySqlConnection(_connectionString);
        var updated = await connection.ExecuteAsync($@"
            UPDATE Users
            SET ConfirmationCodeProtected = @ProtectedCode,
                ConfirmationCodeExpiresAt = @ExpiresAt,
                ConfirmationAttempts = 0
            WHERE UserId = @UserId AND {IsPendingSql};",
            new { UserId = userId, ProtectedCode = protectedCode, ExpiresAt = expiresAt });
        if (updated != 1)
            throw new InvalidOperationException("The confirmation code could not be refreshed.");
    }

    public async Task IncrementConfirmationAttemptsAsync(int userId, string expectedProtectedCode)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.ExecuteAsync($@"
            UPDATE Users
            SET ConfirmationAttempts = ConfirmationAttempts + 1
            WHERE UserId = @UserId
              AND ConfirmationCodeProtected = @ExpectedProtectedCode
              AND ConfirmationAttempts < 5
              AND {IsPendingSql};",
            new { UserId = userId, ExpectedProtectedCode = expectedProtectedCode });
    }

    public async Task<bool> ConfirmUserAsync(int userId, string expectedProtectedCode)
    {
        await using var connection = new MySqlConnection(_connectionString);
        return await connection.ExecuteAsync($@"
            UPDATE Users
            SET EmailConfirmed = TRUE,
                IsActive = TRUE,
                ConfirmationCodeProtected = NULL,
                ConfirmationCodeExpiresAt = NULL,
                ConfirmationAttempts = 0
            WHERE UserId = @UserId
              AND ConfirmationCodeProtected = @ExpectedProtectedCode
              AND ConfirmationAttempts < 5
              AND ConfirmationCodeExpiresAt > UTC_TIMESTAMP()
              AND {IsPendingSql};",
            new { UserId = userId, ExpectedProtectedCode = expectedProtectedCode }) == 1;
    }
}
