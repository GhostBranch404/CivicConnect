using System.Threading.RateLimiting;
using CivicConnect.Core.Data;
using CivicConnect.Core.Middleware;
using CivicConnect.Core.Services;
using CivicConnect.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddDataProtection().SetApplicationName("CivicConnect");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("signup", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                SegmentsPerWindow = 10,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("confirmation", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                SegmentsPerWindow = 10,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddScoped<IRequestRepository>(_ => new RequestRepository(connectionString));
builder.Services.AddScoped<CivicConnect.Core.Data.IUserRepository>(_ => new UserRepository(connectionString));
builder.Services.AddScoped<IAuditHistoryService, AuditHistoryService>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IRequestDetailsRepository>(_ => new RequestDetailsRepository(connectionString));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminRepository>(_ => new AdminRepository(connectionString));
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddSingleton<INotificationService, LogNotificationService>();
builder.Services.AddSingleton<IDeveloperLogRepository>(sp =>
    new DeveloperLogRepository(connectionString, sp.GetRequiredService<ILogger<DeveloperLogRepository>>()));
builder.Services.AddScoped<IUserConfirmationRepository>(_ => new UserRepository(connectionString));
builder.Services.AddScoped<UserRegistrationService>();
builder.Services.AddScoped<IPasswordHasher<CivicUser>, PasswordHasher<CivicUser>>();
// Confirmation emails. Settings come from Email:Host etc. (Azure: Email__Host) or Email_Host / EmailHost.
builder.Services.AddOptions<EmailSmtpOptions>()
    .Configure<IConfiguration>((options, configuration) => EmailSmtpOptions.Bind(options, configuration));
builder.Services.AddScoped<IEmailConfirmationSender, SmtpEmailConfirmationSender>();

var app = builder.Build();

// Warn at startup (without printing any values) if confirmation emails can't be sent.
var missingEmailSettings = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailSmtpOptions>>()
    .Value.MissingSettings();
if (missingEmailSettings.Count > 0)
    app.Logger.LogWarning(
        "Email is not configured, so sign-up confirmation codes can't be sent. Missing: {Missing}",
        string.Join(", ", missingEmailSettings));

// Records server errors in DeveloperLogs so devs can see them on the Logs page. Keep it first.
app.UseMiddleware<ErrorLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseDefaultFiles();   // serves wwwroot/index.html at /
app.UseStaticFiles();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Any non-API, non-file URL returns the UI, so refreshing or deep links never 404.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
