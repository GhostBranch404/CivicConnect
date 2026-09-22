using CivicConnect.Core.Data;
using CivicConnect.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Retrieve the MySQL Connection String from the appsettings.json
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Register Repository and Service Dependencies
builder.Services.AddScoped<IRequestRepository>(provider => new RequestRepository(connectionString));
builder.Services.AddScoped<IRequestService, RequestService>();

var app = builder.Build();

// Pipeline Configuration of HTTP Requests
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();