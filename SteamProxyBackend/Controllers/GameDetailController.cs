using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace SteamProxyBackend.Controllers
{
    [ApiController]
    [Route("api/gamedetail")]
    public class GameDetailController : ControllerBase
    {
        private readonly HttpClient _http;
        private readonly string _steamApiKey;

        public GameDetailController(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _http = httpFactory.CreateClient();
            _steamApiKey = Environment.GetEnvironmentVariable("SteamApiKey")
                ?? config["SteamApiKey"]
                ?? throw new Exception("SteamApiKey not configured.");
        }

        // GET /api/gamedetail/{steamId}/{appId}
        // Returns: lastPlayed + achievements (schema merged with player data)
        [HttpGet("{steamId}/{appId}")]
        public async Task<IActionResult> GetGameDetail(string steamId, int appId)
        {
            try
            {
                // ─── 1. LAST PLAYED — from recently played games ──────────
                DateTime? lastPlayed = null;
                try
                {
                    var url = $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/" +
                              $"?key={_steamApiKey}&steamid={steamId}&count=100";
                    var json = JObject.Parse(await _http.GetStringAsync(url));
                    var games = json["response"]?["games"];

                    if (games != null)
                    {
                        foreach (var g in games)
                        {
                            if (g["appid"]?.Value<int>() != appId) continue;
                            var ts = g["last_played"]?.Value<long>() ?? 0;
                            if (ts > 0)
                                lastPlayed = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
                            break;
                        }
                    }
                }
                catch { }

                // ─── 2. ACHIEVEMENT SCHEMA — names + icons ────────────────
                var schema = new Dictionary<string, SchemaEntry>();
                try
                {
                    var url = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/" +
                              $"?key={_steamApiKey}&appid={appId}&l=en";
                    var json = JObject.Parse(await _http.GetStringAsync(url));
                    var achs = json["game"]?["availableGameStats"]?["achievements"];

                    if (achs != null)
                    {
                        foreach (var a in achs)
                        {
                            var name = a["name"]?.Value<string>() ?? "";
                            if (string.IsNullOrEmpty(name)) continue;
                            schema[name] = new SchemaEntry(
                                a["displayName"]?.Value<string>() ?? "",
                                a["description"]?.Value<string>() ?? "",
                                a["icon"]?.Value<string>() ?? "",
                                a["icongray"]?.Value<string>() ?? ""
                            );
                        }
                    }
                }
                catch { }

                // ─── 3. PLAYER ACHIEVEMENTS — what the player unlocked ────
                var achievements = new List<object>();
                int unlocked = 0;
                try
                {
                    var url = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/" +
                              $"?key={_steamApiKey}&steamid={steamId}&appid={appId}&l=en";
                    var json = JObject.Parse(await _http.GetStringAsync(url));
                    var achs = json["playerstats"]?["achievements"];

                    if (achs != null)
                    {
                        foreach (var a in achs)
                        {
                            var apiName = a["apiname"]?.Value<string>() ?? "";
                            bool isUnlocked = (a["achieved"]?.Value<int>() ?? 0) == 1;
                            if (isUnlocked) unlocked++;

                            DateTime? unlockTime = null;
                            var ts = a["unlocktime"]?.Value<long>() ?? 0;
                            if (ts > 0)
                                unlockTime = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;

                            schema.TryGetValue(apiName, out var s);

                            achievements.Add(new
                            {
                                ApiName = apiName,
                                Name = s?.DisplayName ?? apiName,
                                Description = s?.Description ?? "",
                                IconUrl = s?.Icon ?? "",
                                IconGrayUrl = s?.IconGray ?? "",
                                Unlocked = isUnlocked,
                                UnlockTime = unlockTime
                            });
                        }
                    }
                }
                catch { }

                return Ok(new
                {
                    AppId = appId,
                    LastPlayed = lastPlayed,
                    AchievementsUnlocked = unlocked,
                    AchievementsTotal = achievements.Count,
                    Achievements = achievements
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        private record SchemaEntry(string DisplayName, string Description, string Icon, string IconGray);
    }
}