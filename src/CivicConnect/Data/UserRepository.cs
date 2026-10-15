using CivicConnect.Core.Models;
using Dapper;
using MySqlConnector;

namespace CivicConnect.Core.Data
{
    public class UserRepository : IUserRepository
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
    }
}
