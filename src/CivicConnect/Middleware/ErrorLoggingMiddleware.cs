using System.Text.Json;
using CivicConnect.Core.Data;
using CivicConnect.Core.Models;

namespace CivicConnect.Core.Middleware
{
    // Records what goes wrong on the server in the DeveloperLogs table, so devs can
    // read it on the Logs page:
    //   - unhandled exceptions (Severity 'Error', with the stack trace) -> 500 response
    //   - any other 5xx response                                      (Severity 'Error')
    //   - 401 / 403 responses on the API                              (Severity 'Warning')
    // The browser sends X-CivicConnect-User so we know who hit the problem.
    public class ErrorLoggingMiddleware
    {
        public const string UserHeader = "X-CivicConnect-User";

        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorLoggingMiddleware> _logger;

        public ErrorLoggingMiddleware(RequestDelegate next, ILogger<ErrorLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        private static string ActorOf(HttpContext ctx) =>
            int.TryParse(ctx.Request.Headers[UserHeader].ToString(), out var id) && id > 0 ? $"User:{id}" : "Anonymous";

        private static string Where(HttpContext ctx) => $"{ctx.Request.Method} {ctx.Request.Path}";

        public async Task InvokeAsync(HttpContext ctx, IDeveloperLogRepository logs)
        {
            try
            {
                await _next(ctx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception for {Where}", Where(ctx));

                var logId = await logs.TryWriteAsync(new DeveloperLog
                {
                    Actor = ActorOf(ctx),
                    Action = "SERVER_EXCEPTION",
                    EntityType = "HttpRequest",
                    EntityId = Where(ctx),
                    Severity = "Error",
                    Details = $"{ex.GetType().Name}: {ex.Message}\n\nRequest: {Where(ctx)}{ctx.Request.QueryString}\n\n{ex}",
                    LoggedAt = DateTime.UtcNow
                });

                if (ctx.Response.HasStarted) throw;

                ctx.Response.Clear();
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    message = logId is null
                        ? "Something went wrong on the server. Please try again."
                        : $"Something went wrong on the server. Please try again. (Error reference #{logId})"
                }));
                return;
            }

            var status = ctx.Response.StatusCode;
            var isApi = ctx.Request.Path.StartsWithSegments("/api");
            if (status >= 500 || (isApi && (status == 401 || status == 403)))
            {
                await logs.TryWriteAsync(new DeveloperLog
                {
                    Actor = ActorOf(ctx),
                    Action = status >= 500 ? "SERVER_ERROR" : "ACCESS_DENIED",
                    EntityType = "HttpRequest",
                    EntityId = Where(ctx),
                    Severity = status >= 500 ? "Error" : "Warning",
                    Details = $"{Where(ctx)} returned {status}.",
                    LoggedAt = DateTime.UtcNow
                });
            }
        }
    }
}
