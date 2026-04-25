using Microsoft.EntityFrameworkCore;
using SteamProxyBackend.Data;


var builder = WebApplication.CreateBuilder(args);

// ─── DATABASE CONNECTION ──────────────────────────────────────────────────────
// Supabase dáva URL vo formáte postgresql://user:pass@host:port/db
// Npgsql potrebuje: Host=...;Database=...;Username=...;Password=...
// Preto konvertujeme — that's why we convert

var rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new Exception("DATABASE_URL environment variable not set.");

var connectionString = ConvertSupabaseUrl(rawConnectionString);

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ─── AUTO MIGRATE ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ─── MIDDLEWARE ───────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowRiftApp");
app.UseAuthorization();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Run($"http://0.0.0.0:{port}");

// ─── HELPER — converts postgresql:// URL to Npgsql format ────────────────────
static string ConvertSupabaseUrl(string url)
{
    try
    {
        // If it's already in Npgsql format (Host=...) return as is
        // Ak je už v Npgsql formáte, vrátime tak ako je
        if (!url.StartsWith("postgresql://") && !url.StartsWith("postgres://"))
            return url;

        var uri = new Uri(url);

        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');
        var username = uri.UserInfo.Split(':')[0];
        var password = uri.UserInfo.Split(':')[1];

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }
    catch
    {
        // If conversion fails — return original and let Npgsql try
        return url;
    }
}