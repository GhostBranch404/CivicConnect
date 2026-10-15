using System.Threading.RateLimiting;
using CivicConnect.Core.Data;
using CivicConnect.Core.Services;
using CivicConnect.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

// Anchor the content root to the folder the app was published to, so wwwroot (the UI)
// is found no matter which working directory Azure App Service starts the process from.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
});

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
builder.Services.AddScoped<IUserRepository>(_ => new UserRepository(connectionString));
builder.Services.AddScoped<IAuditHistoryService, AuditHistoryService>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<UserRegistrationService>();
builder.Services.AddScoped<IPasswordHasher<CivicUser>, PasswordHasher<CivicUser>>();
builder.Services.Configure<EmailSmtpOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<TwilioSmsOptions>(builder.Configuration.GetSection("Sms"));
builder.Services.AddScoped<IEmailConfirmationSender, SmtpEmailConfirmationSender>();
builder.Services.AddHttpClient<ISmsConfirmationSender, TwilioSmsConfirmationSender>();

var app = builder.Build();

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
