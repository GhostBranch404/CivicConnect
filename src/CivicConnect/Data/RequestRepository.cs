using CivicConnect.Core.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace CivicConnect.Core.Data
{
    public class RequestRepository : IRequestRepository
    {
        private readonly string Con;

        public RequestRepository(string connection)
        {
            Con = connection;
        }

        public async Task<int> CreateNewRequest(ServiceRequest request)
        {
            using var connection = new MySqlConnection(Con);
            string sql = @"INSERT INTO ServiceRequests (Username, Category, Description, RequestStatus, RequestDate) 
                           VALUES (@Username, @Category, @Description, @RequestStatus, @RequestDate);
                           SELECT LAST_INSERT_ID();";

            return await connection.ExecuteScalarAsync<int>(sql, request);
        }

        public async Task<ServiceRequest?> GetRequestById(int id)
        {
            using var connection = new MySqlConnection(Con);
            string sql = @"SELECT * FROM ServiceRequests WHERE RequestId = @Id;";
            return await connection.QuerySingleOrDefaultAsync<ServiceRequest>(sql, new { Id = id });
        }

        public async Task<IEnumerable<ServiceRequest>> GetAllRequests()
        {
            using var connection = new MySqlConnection(Con);
            string sql = @"SELECT * FROM ServiceRequests ORDER BY RequestDate DESC;";
            return await connection.QueryAsync<ServiceRequest>(sql);
        }

        public async Task<int?> GetStatusIdByNameAsync(string statusName)
        {
            using var connection = new MySqlConnection(Con);
            string sql = @"SELECT StatusId FROM RequestStatuses WHERE StatusName = @StatusName;";
            return await connection.QuerySingleOrDefaultAsync<int?>(sql, new { StatusName = statusName });
        }

        // public async Task RecordRequestHistoryAsync(RequestHistory history)
        // {
        //     using var connection = new MySqlConnection(Con);
        //     string sql = @"INSERT INTO RequestHistories (RequestId, Status, ChangedByUserId, Notes, ChangedAt)
        //                    VALUES (@RequestId, @Status, @ChangedByUserId, @Notes, @ChangedAt);";
        //     await connection.ExecuteAsync(sql, history);
        // }

        public async Task<bool> UpdateRequestStatus(
            int id,
            string status,
            int changedByUserId,
            int expectedVersion,
            string? notes = null)
        {
            using var connection = new MySqlConnection(Con);
            string sql = @"UPDATE ServiceRequests 
                           SET RequestStatus = @Status, Version = Version + 1 
                           WHERE RequestId = @Id AND Version = @ExpectedVersion;";
                           
            int updatedRows = await connection.ExecuteAsync(sql, new { 
                Id = id, 
                Status = status, 
                ExpectedVersion = expectedVersion 
            });

            return updatedRows > 0;
        }
    }
}