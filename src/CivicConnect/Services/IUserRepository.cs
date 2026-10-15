using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services;

public interface IUserRepository
{
    Task<int> CreatePendingUserAsync(CivicUser user);
    Task<CivicUser?> FindByConfirmationDestinationAsync(string method, string destination);
    Task UpdateConfirmationCodeAsync(int userId, string protectedCode, DateTime expiresAt);
    Task IncrementConfirmationAttemptsAsync(int userId, string expectedProtectedCode);
    Task<bool> ConfirmUserAsync(int userId, string method, string expectedProtectedCode);
}
