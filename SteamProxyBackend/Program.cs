using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Pridáme HttpClient na volanie Steam
builder.Services.AddHttpClient();

var app = builder.Build();

// === STEAM API PROXY (univerzálny pre všetky Steam volania) ===
app.MapPost("/api/steam", async (HttpRequest req, IHttpClientFactory clientFactory, IConfiguration config) =>
{
    var steamKey = config["SteamApiKey"];
    if (string.IsNullOrEmpty(steamKey))
        return Results.BadRequest("Steam API key nie je nastavený.");

    try
    {
        var requestBody = await JsonSerializer.DeserializeAsync<SteamProxyRequest>(req.Body);

        var client = clientFactory.CreateClient();

        // Zostaví URL napr. https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0001/
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

// === Pomocná trieda (pridaj na úplný koniec súboru) ===
public class SteamProxyRequest
{
    public string Interface { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Version { get; set; } = "v0001";
    public Dictionary<string, string> Parameters { get; set; } = new();
}
