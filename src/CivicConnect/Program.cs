using CivicConnect.Core.Data;
using CivicConnect.Core.Middleware;
using CivicConnect.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddScoped<IRequestRepository>(_ => new RequestRepository(connectionString));
builder.Services.AddScoped<IAuditHistoryService, AuditHistoryService>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IUserRepository>(_ => new UserRepository(connectionString));
builder.Services.AddScoped<IRequestDetailsRepository>(_ => new RequestDetailsRepository(connectionString));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminRepository>(_ => new AdminRepository(connectionString));
builder.Services.AddScoped<IAdminService, AdminService>();
// Swap for an email-based implementation once the email API is ready.
builder.Services.AddSingleton<INotificationService, LogNotificationService>();
builder.Services.AddSingleton<IDeveloperLogRepository>(sp =>
    new DeveloperLogRepository(connectionString, sp.GetRequiredService<ILogger<DeveloperLogRepository>>()));

var app = builder.Build();

// Records server errors in DeveloperLogs so devs can see them on the Logs page. Keep it first.
app.UseMiddleware<ErrorLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();   // serves wwwroot/index.html at /
app.UseStaticFiles();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Any non-API, non-file URL returns the UI, so refreshing or deep links never 404.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
