using CivicConnect.Core.Models;

namespace CivicConnect.Core.Services
{
    // Sends messages to users. The current implementation only writes to the log;
    // replace LogNotificationService with an email-based implementation once the
    // email API is available (register it in Program.cs).
    public interface INotificationService
    {
        Task SendAsync(IEnumerable<User> recipients, string subject, string body);
    }

    public class LogNotificationService : INotificationService
    {
        private readonly ILogger<LogNotificationService> _logger;

        public LogNotificationService(ILogger<LogNotificationService> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(IEnumerable<User> recipients, string subject, string body)
        {
            foreach (var r in recipients)
            {
                _logger.LogInformation(
                    "[Notification not sent - email not configured] To: {Email} | Subject: {Subject} | {Body}",
                    r.Email, subject, body);
            }
            return Task.CompletedTask;
        }
    }
}
