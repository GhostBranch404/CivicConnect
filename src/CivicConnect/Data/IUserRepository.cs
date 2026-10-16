using CivicConnect.Core.Models;

namespace CivicConnect.Core.Data
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<IEnumerable<User>> GetAllAsync(string? role = null);
        Task<int> CreateAsync(User user);
    }
}
