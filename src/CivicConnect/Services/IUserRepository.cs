using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services;

public interface IUserConfirmationRepository
{
    /// <summary>
    /// Saves a new unconfirmed account (or refreshes an existing unconfirmed sign-up for the same
    /// email) inside a database transaction, then calls <paramref name="deliverCode"/>. The
    /// transaction only commits if <paramref name="deliverCode"/> succeeds, so no account is
    /// created when the confirmation email can't be sent.
    /// Throws <see cref="AccountAlreadyExistsException"/> if a confirmed, active or admin-created
    /// account already uses the email.
    /// </summary>
    Task<int> CreatePendingUserAsync(CivicUser user, Func<Task> deliverCode);

    Task<CivicUser?> FindByEmailAsync(string email);
    Task UpdateConfirmationCodeAsync(int userId, string protectedCode, DateTime expiresAt);
    Task IncrementConfirmationAttemptsAsync(int userId, string expectedProtectedCode);
    Task<bool> ConfirmUserAsync(int userId, string expectedProtectedCode);
}
