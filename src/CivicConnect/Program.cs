using CivicConnect.Core.Data;
using CivicConnect.Core.Services;

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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddScoped<IRequestRepository>(_ => new RequestRepository(connectionString));
builder.Services.AddScoped<IAuditHistoryService, AuditHistoryService>();
builder.Services.AddScoped<IRequestService, RequestService>();

var app = builder.Build();

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
