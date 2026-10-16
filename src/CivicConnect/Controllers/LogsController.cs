using CivicConnect.Core.Data;
using CivicConnect.Core.Middleware;
using CivicConnect.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Core.Controllers
{
    [ApiController]
    [Route("api/logs")]
    public class LogsController : ControllerBase
    {
        private static readonly HashSet<string> Severities = new(StringComparer.OrdinalIgnoreCase)
            { "Error", "Warning", "Information" };

        private readonly IDeveloperLogRepository _logs;
        private readonly IAdminRepository _users;

        public LogsController(IDeveloperLogRepository logs, IAdminRepository users)
        {
            _logs = logs;
            _users = users;
        }

        // GET /api/logs?actingUserId=7&severity=Error&search=login&limit=200   (Dev only)
        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int actingUserId, [FromQuery] string? severity, [FromQuery] string? search, [FromQuery] int limit = 200)
        {
            var user = await _users.GetUserByIdAsync(actingUserId);
            if (user is null || !user.IsActive || user.Role != UserRoles.Dev)
                return StatusCode(StatusCodes.Status403Forbidden, new { Message = "Only devs can view the logs." });

            var sev = string.IsNullOrWhiteSpace(severity) ? null : severity.Trim();
            if (sev is not null && !Severities.Contains(sev))
                return BadRequest(new { Message = "Severity must be Error, Warning or Information." });

            var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var entries = await _logs.SearchAsync(sev, term, Math.Clamp(limit, 1, 500));
            var last24h = await _logs.CountsSinceAsync(DateTime.UtcNow.AddHours(-24));
            return Ok(new { Entries = entries, Last24Hours = last24h });
        }

        // POST /api/logs/client  - the browser reports JavaScript errors here.
        [HttpPost("client")]
        public async Task<IActionResult> Client([FromBody] ClientErrorReport report)
        {
            if (string.IsNullOrWhiteSpace(report.Message))
                return BadRequest(new { Message = "A message is required." });

            var userHeader = Request.Headers[ErrorLoggingMiddleware.UserHeader].ToString();
            var actor = int.TryParse(userHeader, out var id) && id > 0 ? $"User:{id}" : "Anonymous";

            await _logs.TryWriteAsync(new DeveloperLog
            {
                Actor = actor,
                Action = "BROWSER_ERROR",
                EntityType = "Browser",
                EntityId = report.Page,
                Severity = "Error",
                Details = $"{report.Message}\n\nPage: {report.Page}\nBrowser: {Request.Headers.UserAgent}\n\n{report.Stack}",
                LoggedAt = DateTime.UtcNow
            });
            return NoContent();
        }
    }

    public class ClientErrorReport
    {
        public string Message { get; set; } = string.Empty;
        public string? Stack { get; set; }
        public string? Page { get; set; }
    }
}
