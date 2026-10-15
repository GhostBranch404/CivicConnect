using CivicConnect.Core.Models;
using Dapper;
using MySqlConnector;

namespace CivicConnect.Core.Data
{
    public class DeveloperLogRepository : IDeveloperLogRepository
    {
        private readonly string _connectionString;
        private readonly string _writeConnectionString;
        private readonly ILogger<DeveloperLogRepository> _logger;

        public DeveloperLogRepository(string connectionString, ILogger<DeveloperLogRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
            // Short timeout for writes so a database outage can't stall every failing request.
            _writeConnectionString = new MySqlConnectionStringBuilder(connectionString) { ConnectionTimeout = 3 }.ConnectionString;
        }

        private static string? Cut(string? s, int max) => s is null || s.Length <= max ? s : s[..max];

        public async Task<long?> TryWriteAsync(DeveloperLog log)
        {
            try
            {
                await using var connection = new MySqlConnection(_writeConnectionString);
                return await connection.ExecuteScalarAsync<long>(@"
                    INSERT INTO DeveloperLogs (RequestId, Actor, Action, EntityType, EntityId, Severity, Details, LoggedAt)
                    VALUES (@RequestId, @Actor, @Action, @EntityType, @EntityId, @Severity, @Details, @LoggedAt);
                    SELECT LAST_INSERT_ID();",
                    new
                    {
                        log.RequestId,
                        Actor = Cut(log.Actor, 150),
                        Action = Cut(log.Action, 100),
                        EntityType = Cut(log.EntityType, 100),
                        EntityId = Cut(log.EntityId, 100),
                        Severity = Cut(log.Severity, 30),
                        Details = Cut(log.Details, 60000),
                        log.LoggedAt
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not write to DeveloperLogs. Original entry: {Action} {Details}", log.Action, log.Details);
                return null;
            }
        }

        public async Task<IEnumerable<DeveloperLog>> SearchAsync(string? severity, string? search, int limit)
        {
            await using var connection = new MySqlConnection(_connectionString);
            const string sql = @"
                SELECT LogId, RequestId, Actor, Action, EntityType, EntityId, Severity, Details, LoggedAt
                FROM DeveloperLogs
                WHERE (@Severity IS NULL OR Severity = @Severity)
                  AND (@Search IS NULL
                       OR Action LIKE CONCAT('%', @Search, '%')
                       OR Actor LIKE CONCAT('%', @Search, '%')
                       OR EntityId LIKE CONCAT('%', @Search, '%')
                       OR Details LIKE CONCAT('%', @Search, '%'))
                ORDER BY LoggedAt DESC, LogId DESC
                LIMIT @Limit;";
            return await connection.QueryAsync<DeveloperLog>(sql, new { Severity = severity, Search = search, Limit = limit });
        }

        public async Task<IEnumerable<SeverityCount>> CountsSinceAsync(DateTime since)
        {
            await using var connection = new MySqlConnection(_connectionString);
            return await connection.QueryAsync<SeverityCount>(@"
                SELECT Severity, COUNT(*) AS Count
                FROM DeveloperLogs
                WHERE LoggedAt >= @Since
                GROUP BY Severity;", new { Since = since });
        }
    }
}
