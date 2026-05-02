using Microsoft.EntityFrameworkCore;
using SteamProxyBackend.Data;


var builder = WebApplication.CreateBuilder(args);

// ─── DATABASE CONNECTION ──────────────────────────────────────────────────────

string connectionString;

var dbHost = Environment.GetEnvironmentVariable("DB_HOST");

if (!string.IsNullOrEmpty(dbHost))
{
    // Use separate env variables — jednotlivé premenné
    var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "6543";
    var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
    var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "";
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

    connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};SSL Mode=Require;Trust Server Certificate=true;Timeout=60;Command Timeout=60;Pooling=false";
}
else
{
    // Fallback to DATABASE_URL
    var rawUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new Exception("No database connection configured.");

    connectionString = ConvertSupabaseUrl(rawUrl);
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowRiftApp", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers()
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ─── CREATE TABLES ────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        Console.WriteLine("Database connected successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database setup error: {ex.Message}");
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowRiftApp");
app.UseAuthorization();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Run($"http://0.0.0.0:{port}");

// ─── HELPER ───────────────────────────────────────────────────────────────────
static string ConvertSupabaseUrl(string url)
{
    try
    {
        if (!url.StartsWith("postgresql://") && !url.StartsWith("postgres://"))
            return url;

        var uri = new Uri(url);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 6543;
        var database = uri.AbsolutePath.TrimStart('/');
        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo[0];
        var password = userInfo[1];

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;Timeout=60;Command Timeout=60;Pooling=false";
    }
    catch
    {
        return url;
    }
}