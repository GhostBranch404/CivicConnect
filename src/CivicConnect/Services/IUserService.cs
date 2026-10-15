using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services
{
    public interface IUserService
    {
        Task<User> RegisterAsync(RegisterRequest request);
        Task<User?> LoginAsync(LoginRequest request);
        Task<IEnumerable<User>> GetUsersAsync(string? role = null);
    }
}
