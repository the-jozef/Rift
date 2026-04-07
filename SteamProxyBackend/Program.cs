using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors();

app.MapGet("/", () => "Rift Steam Proxy is running.");

app.MapPost("/api/steam", async (HttpRequest req, IHttpClientFactory clientFactory, IConfiguration config) =>
{
    var steamKey = Environment.GetEnvironmentVariable("STEAM_API_KEY")
                   ?? config["SteamApiKey"];

    if (string.IsNullOrEmpty(steamKey))
        return Results.BadRequest("Steam API key nie je nastavený.");

    try
    {
        var requestBody = await JsonSerializer.DeserializeAsync<SteamProxyRequest>(req.Body);

        if (requestBody == null)
            return Results.BadRequest("Request body je null.");

        var client = clientFactory.CreateClient();

        string baseUrl = $"https://api.steampowered.com/{requestBody.Interface}/{requestBody.Method}/{requestBody.Version}/";

        var queryParams = requestBody.Parameters
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}")
            .ToList();
        queryParams.Add($"key={steamKey}");

        string fullUrl = baseUrl + "?" + string.Join("&", queryParams);

        var response = await client.GetAsync(fullUrl);
        var content = await response.Content.ReadAsStringAsync();

        return Results.Content(content, "application/json");
    }
    catch (Exception ex)
    {
        return Results.BadRequest("Chyba: " + ex.Message);
    }
});

// Render uses PORT env variable (default 10000)
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Run($"http://0.0.0.0:{port}");

public class SteamProxyRequest
{
    public string Interface { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Version { get; set; } = "v0001";
    public Dictionary<string, string> Parameters { get; set; } = new();
}