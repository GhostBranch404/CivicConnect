using System.Text.RegularExpressions;
using CivicConnect.Core.Data;
using CivicConnect.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace CivicConnect.Core.Services
{
    public class UserService : IUserService
    {
        private static readonly Regex EmailPattern =
            new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);

        private readonly IUserRepository _users;
        private readonly PasswordHasher<User> _hasher = new();

        public UserService(IUserRepository users)
        {
            _users = users;
        }

        public async Task<User> RegisterAsync(RegisterRequest request)
        {
            var name = request.FullName?.Trim() ?? string.Empty;
            var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var password = request.Password ?? string.Empty;

            if (name.Length == 0)
                throw new ArgumentException("Please enter your full name.");
            if (!EmailPattern.IsMatch(email))
                throw new ArgumentException("Please enter a valid email address.");
            if (password.Length < 8)
                throw new ArgumentException("Password must be at least 8 characters.");

            if (await _users.GetByEmailAsync(email) is not null)
                throw new InvalidOperationException("An account with this email already exists.");

            // Self-registration always creates a requester; staff/manager roles are granted by an admin.
            var user = new User
            {
                FullName = name,
                Email = email,
                Role = UserRoles.Requester,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = _hasher.HashPassword(user, password);
            user.UserId = await _users.CreateAsync(user);
            user.PasswordHash = null;
            return user;
        }

        public async Task<User?> LoginAsync(LoginRequest request)
        {
            var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var user = await _users.GetByEmailAsync(email);

            if (user is null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
                return null;

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? string.Empty);
            if (result == PasswordVerificationResult.Failed)
                return null;

            user.PasswordHash = null;
            return user;
        }

        public Task<IEnumerable<User>> GetUsersAsync(string? role = null) =>
            _users.GetAllAsync(string.IsNullOrWhiteSpace(role) ? null : role);
    }
}
