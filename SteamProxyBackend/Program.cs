using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

// Pridaj CORS aby WPF mohla volať server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors();

app.MapPost("/api/steam", async (HttpRequest req, IHttpClientFactory clientFactory, IConfiguration config) =>
{
    // Najprv skúsi Environment Variable, potom appsettings.json
    var steamKey = Environment.GetEnvironmentVariable("STEAM_API_KEY")
                   ?? config["SteamApiKey"];

    if (string.IsNullOrEmpty(steamKey))
        return Results.BadRequest("Steam API key nie je nastavený.");

    try
    {
        var requestBody = await JsonSerializer.DeserializeAsync<SteamProxyRequest>(req.Body);
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

app.Run();

public class SteamProxyRequest
{
    public string Interface { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Version { get; set; } = "v0001";
    public Dictionary<string, string> Parameters { get; set; } = new();
}