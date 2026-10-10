using CivicConnect.Core.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace CivicConnect.Core.Data
{
    public interface IRequestRepository
    {
        Task<int> CreateNewRequest(ServiceRequest request);
        Task<ServiceRequest?> GetRequestById(int id);
        Task<IEnumerable<ServiceRequest>> GetAllRequests();
        Task<bool> UpdateRequestStatus(int id, string status);
    }

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
                           VALUES (@Username, @Category, @Description, @RequestStatus, @RequestDate);";

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
            string sql = @"SELECT * FROM ServiceRequests 
                        ORDER BY CreatedAt DESC;";
            return await connection.QueryAsync<ServiceRequest>(sql);
        }

        public async Task<bool> UpdateRequestStatus(int id, string status)
        {
            using var connection = new MySqlConnection(Con);
            string sql = "UPDATE ServiceRequests SET RequestStatus = @Status WHERE RequestId = @Id;";
            int updatedRows = await connection.ExecuteAsync(sql, new { Id = id, Status = status });
            return updatedRows > 0;
        }
    }
}