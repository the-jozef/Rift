using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace SteamProxyBackend.Controllers
{
    [ApiController]
    [Route("api/steam")]
    public class SteamController : ControllerBase
    {
        private readonly HttpClient _http;
        private readonly string _steamApiKey;

        private static readonly ConcurrentDictionary<string, DateTime> _lastRequestTime = new();
        private const int RateLimitSeconds = 2;

        // ─── KURÁTORSKÝ ZOZNAM POPULÁRNYCH HIER ──────────────────────────
        // Curated list of popular well-known games with full screenshots
        // Zoradené podľa popularity — sorted by popularity
        private static readonly int[] CuratedAppIds = new[]
        {
            // AAA aktuálne — current AAA
            2322010,  // Black Myth: Wukong
            1091500,  // Cyberpunk 2077
            1172470,  // Apex Legends
            1245620,  // Elden Ring
            1086940,  // Baldur's Gate 3
            2358720,  // Black Flag Resynced (AC Black Flag)
            3240220,  // GTA V Enhanced
            1551360,  // Forza Horizon 5
            1850570,  // Hogwarts Legacy
            1716740,  // STALKER 2
            2767030,  // Marvel Rivals
            3228840,  // Beiman (Dynasty Warriors Origins)
            2369390,  // Snowrunner (MudRunner 2)
            1174180,  // Red Dead Redemption 2
            1623730,  // Palworld
            990080,   // Hogwarts Legacy (Steam)
            1063730,  // New World
            2311740,  // Warhammer 40K Space Marine 2
            2358720,  // Assassin's Creed Shadows
            1817070,  // Marvel's Spider-Man Remastered
 
            // Evergreen populárne — evergreen popular
            730,      // CS2
            570,      // Dota 2
            578080,   // PUBG
            1222670,  // The Sims 4
            252490,   // Rust
            346110,   // ARK: Survival Evolved
            381210,   // Dead by Daylight
            1382330,  // Fortnite
            813780,   // AOE2 DE
            1145360,  // Hades
            814380,   // Sekiro
            1237970,  // Titanfall 2
            374320,   // Dark Souls 3
            292030,   // Witcher 3
            367520,   // Hollow Knight
            1449850,  // Yu-Gi-Oh Master Duel
            271590,   // GTA V (original)
            1446780,  // Monster Hunter Rise
            2050650,  // Resident Evil 4 Remake
            1938090,  // Call of Duty HQ
        };

        // ─── 18+ FILTER ───────────────────────────────────────────────────

        private static readonly string[] AdultKeywords = new[]
        {
            "hentai", "nude", "naked", "erotic", "xxx", "porn",
            "lewd", "nsfw", "18+", "adult", "fuck", "futa",
            "cumming", "horny", "🔞", "ecchi"
        };

        private static bool HasAdultName(string name)
        {
            var lower = name.ToLowerInvariant();
            return AdultKeywords.Any(k => lower.Contains(k));
        }

        // IDs: 1=Nudity/Sexual, 3=Adult Only Sexual, 4=Frequent Nudity/Sexual
        // CoD ID 2=Violence, ID 5=Mature — prechádza / passes
        private static bool HasExplicitContent(JToken? data)
        {
            if (data == null) return false;
            var ids = data["content_descriptors"]?["ids"]?.ToObject<List<int>>() ?? new List<int>();
            return ids.Contains(1) || ids.Contains(3) || ids.Contains(4);
        }

        public SteamController(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _http = httpFactory.CreateClient();
            _steamApiKey = Environment.GetEnvironmentVariable("SteamApiKey")
                ?? config["SteamApiKey"]
                ?? throw new Exception("SteamApiKey not configured.");
        }

        private bool IsRateLimited(string ip)
        {
            if (_lastRequestTime.TryGetValue(ip, out var last))
                if ((DateTime.UtcNow - last).TotalSeconds < RateLimitSeconds) return true;
            _lastRequestTime[ip] = DateTime.UtcNow;
            return false;
        }

        private string GetClientIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // ─── PLAYER SUMMARY ───────────────────────────────────────────────

        [HttpGet("player/{steamId}")]
        public async Task<IActionResult> GetPlayerSummary(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_steamApiKey}&steamids={steamId}";
                var response = await _http.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                var players = data?.response?.players;

                if (players == null || players.Count == 0)
                    return NotFound(new { Message = "Steam player not found." });

                var player = players[0];
                return Ok(new
                {
                    SteamId = (string)player.steamid,
                    Username = (string)player.personaname,
                    AvatarUrl = (string)player.avatarfull,
                    ProfileUrl = (string)player.profileurl
                });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── OWNED GAMES / LIBRARY ────────────────────────────────────────

        [HttpGet("library/{steamId}")]
        public async Task<IActionResult> GetOwnedGames(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={_steamApiKey}&steamid={steamId}&include_appinfo=true&include_played_free_games=true";
                var response = await _http.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                var games = data?.response?.games;

                if (games == null) return Ok(new { Games = new List<object>() });

                var result = new List<object>();
                foreach (var game in games)
                {
                    result.Add(new
                    {
                        AppId = (int)game.appid,
                        Name = (string)game.name,
                        PlaytimeMinutes = (int)game.playtime_forever,
                        IconUrl = $"https://media.steampowered.com/steamcommunity/public/images/apps/{game.appid}/{game.img_icon_url}.jpg",
                        HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{game.appid}/header.jpg"
                    });
                }

                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── ACHIEVEMENTS ─────────────────────────────────────────────────

        [HttpGet("achievements/{appId}/{steamId}")]
        public async Task<IActionResult> GetAchievements(int appId, string steamId)
        {
            try
            {
                // Schema — ikony + názvy (vždy funguje)
                var schemaUrl = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={_steamApiKey}&appid={appId}&l=en";
                var schemaJson = await _http.GetStringAsync(schemaUrl);
                var schemaData = JObject.Parse(schemaJson);
                var schemaAchs = schemaData["game"]?["availableGameStats"]?["achievements"];

                var iconLookup = new Dictionary<string, (string icon, string iconGray)>();
                if (schemaAchs != null)
                {
                    foreach (var a in schemaAchs)
                    {
                        var name = a["name"]?.Value<string>() ?? "";
                        iconLookup[name] = (
                            a["icon"]?.Value<string>() ?? "",
                            a["icongray"]?.Value<string>() ?? ""
                        );
                    }
                }

                // Player stats — unlock status
                // Funguje aj pre private profil s vlastníckym API kľúčom
                var statsUrl = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={_steamApiKey}&appid={appId}&steamid={steamId}&l=en";

                List<object> result = new();
                int unlocked = 0;

                try
                {
                    var statsJson = await _http.GetStringAsync(statsUrl);
                    var statsData = JObject.Parse(statsJson);
                    var playerAchs = statsData["playerstats"]?["achievements"];

                    if (playerAchs != null)
                    {
                        foreach (var a in playerAchs)
                        {
                            var apiName = a["apiname"]?.Value<string>() ?? "";
                            var achieved = a["achieved"]?.Value<int>() == 1;
                            var unlockTime = a["unlocktime"]?.Value<long>() ?? 0;
                            if (achieved) unlocked++;

                            iconLookup.TryGetValue(apiName, out var icons);

                            result.Add(new
                            {
                                ApiName = apiName,
                                Name = a["name"]?.Value<string>() ?? "",
                                Description = a["description"]?.Value<string>() ?? "",
                                IconUrl = icons.icon ?? "",
                                IconGrayUrl = icons.iconGray ?? "",
                                Unlocked = achieved,
                                UnlockTime = unlockTime > 0
                                    ? DateTimeOffset.FromUnixTimeSeconds(unlockTime).UtcDateTime
                                    : (DateTime?)null
                            });
                        }
                    }
                }
                catch
                {
                    // Private profil alebo hra bez achievementov
                    // Vráť len schema bez unlock statusu
                    foreach (var kvp in iconLookup)
                    {
                        result.Add(new
                        {
                            ApiName = kvp.Key,
                            Name = "",
                            Description = "",
                            IconUrl = kvp.Value.icon,
                            IconGrayUrl = kvp.Value.iconGray,
                            Unlocked = false,
                            UnlockTime = (DateTime?)null
                        });
                    }
                }

                return Ok(new { Achievements = result, Total = result.Count, Unlocked = unlocked });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        [HttpGet("schema/{appId}")]
        public async Task<IActionResult> GetGameSchema(int appId)
        {
            try
            {
                var url = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={_steamApiKey}&appid={appId}&l=en";
                var response = await _http.GetStringAsync(url);
                var data = JObject.Parse(response);

                var achievements = data["game"]?["availableGameStats"]?["achievements"];
                if (achievements == null)
                    return Ok(new { Achievements = new List<object>() });

                var result = achievements.Select(a => new
                {
                    ApiName = a["name"]?.Value<string>() ?? "",
                    DisplayName = a["displayName"]?.Value<string>() ?? "",
                    Description = a["description"]?.Value<string>() ?? "",
                    IconUrl = a["icon"]?.Value<string>() ?? "",
                    IconGrayUrl = a["icongray"]?.Value<string>() ?? ""
                }).ToList();

                return Ok(new { Achievements = result });
            }
            catch { return Ok(new { Achievements = new List<object>() }); }
        }

        // ─── PLAYER ACHIEVEMENTS — unlock status (vyžaduje public profil) ─────
        [HttpGet("playerstats/{appId}/{steamId}")]
        public async Task<IActionResult> GetPlayerStats(int appId, string steamId)
        {
            try
            {
                var url = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={_steamApiKey}&appid={appId}&steamid={steamId}&l=en";
                var response = await _http.GetStringAsync(url);
                var data = JObject.Parse(response);

                var achievements = data["playerstats"]?["achievements"];
                if (achievements == null)
                    return Ok(new { Achievements = new List<object>() });

                var result = achievements.Select(a => new
                {
                    ApiName = a["apiname"]?.Value<string>() ?? "",
                    Achieved = a["achieved"]?.Value<int>() == 1,
                    UnlockTime = a["unlocktime"]?.Value<long>() ?? 0
                }).ToList();

                return Ok(new { Achievements = result });
            }
            catch
            {
                // Private profile — vráť prázdny zoznam bez chyby
                return Ok(new { Achievements = new List<object>() });
            }
        }

        // ─── WISHLIST ─────────────────────────────────────────────────────


        [HttpGet("wishlist/{steamId}")]
        public async Task<IActionResult> GetWishlist(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                // ─── 1. Získaj ID zoznam ───────────────────────────────────────────
                var listUrl = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?key={_steamApiKey}&steamid={steamId}";
                var listJson = JObject.Parse(await _http.GetStringAsync(listUrl));
                var items = listJson["response"]?["items"] as JArray;

                if (items == null || !items.Any())
                    return Ok(new { Games = new List<object>() });

                // Vytvor lookup: appId → dateAdded
                var dateAddedLookup = items
                    .Where(i => (i["appid"]?.Value<int>() ?? 0) > 0)
                    .ToDictionary(
                        i => i["appid"]!.Value<int>(),
                        i => i["date_added"]?.Value<long>() ?? 0);

                var appIds = dateAddedLookup.Keys.ToList();

                // ─── 2. Batch appdetails — 10 hier per request ────────────────────
                var result = new List<object>();
                int batchSize = 10;

                for (int i = 0; i < appIds.Count; i += batchSize)
                {
                    var batch = appIds.Skip(i).Take(batchSize).ToList();
                    var batchResults = await FetchBatchAppDetailsAsync(batch, dateAddedLookup);
                    result.AddRange(batchResults);

                    // Malý delay medzi batchmi — ochrana rate limitu
                    if (i + batchSize < appIds.Count)
                        await Task.Delay(500);
                }

                // ─── 3. Zoraď: vydané od najnovšie pridaných → nevydané na konci ──
                result = result
                    .Cast<dynamic>()
                    .OrderBy(g => !(bool)g.IsReleased)
                    .ThenByDescending(g => (long)g.DateAddedUnix)
                    .Cast<object>()
                    .ToList();

                return Ok(new { Games = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        private async Task<object?> FetchAppDetailsAsync(int appId, long dateAdded)
        {
            try
            {
                var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=us&l=en";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);

                var data = json[appId.ToString()]?["data"];
                if (data == null) return null;

                string name = data["name"]?.Value<string>() ?? "";
                if (HasAdultName(name) || HasExplicitContent(data)) return null;

                // ─── Tags ─────────────────────────────────────────────────────────
                var tags = data["categories"] is JArray cats
                    ? cats.Select(c => c["description"]?.Value<string>() ?? "")
                          .Where(t => !string.IsNullOrEmpty(t))
                          .Take(5)
                          .ToList()
                    : new List<string>();

                // Ak categories prázdne — skús genres
                if (!tags.Any())
                {
                    tags = data["genres"] is JArray genres
                        ? genres.Select(g => g["description"]?.Value<string>() ?? "")
                                .Where(t => !string.IsNullOrEmpty(t))
                                .Take(5)
                                .ToList()
                        : new List<string>();
                }

                // ─── Reviews ──────────────────────────────────────────────────────
                var reviewDesc = "";
                var reviewCss = "";
                var reviews = data["ratings"]?["steam_germany"] ?? data["ratings"]?["overall"];
                if (reviews != null)
                {
                    int pos = reviews["positive"]?.Value<int>() ?? 0;
                    int total = (reviews["positive"]?.Value<int>() ?? 0) +
                                (reviews["negative"]?.Value<int>() ?? 0);

                    if (total > 0)
                    {
                        double pct = (double)pos / total * 100;
                        (reviewDesc, reviewCss) = pct switch
                        {
                            >= 95 => ("Overwhelmingly Positive", "overwhelmingPositive"),
                            >= 80 => ("Very Positive", "positive"),
                            >= 70 => ("Mostly Positive", "positive"),
                            >= 40 => ("Mixed", "mixed"),
                            >= 20 => ("Mostly Negative", "negative"),
                            _ => ("Overwhelmingly Negative", "overwhelminglyNegative")
                        };
                    }
                }

                // Fallback — metacritic
                if (string.IsNullOrEmpty(reviewDesc))
                {
                    int meta = data["metacritic"]?["score"]?.Value<int>() ?? 0;
                    if (meta >= 75) { reviewDesc = "Very Positive"; reviewCss = "positive"; }
                    else if (meta >= 50) { reviewDesc = "Mixed"; reviewCss = "mixed"; }
                    else if (meta > 0) { reviewDesc = "Negative"; reviewCss = "negative"; }
                }

                if (string.IsNullOrEmpty(reviewDesc))
                    reviewDesc = "No Reviews";

                // ─── Release date ──────────────────────────────────────────────────
                bool isReleased = !(data["release_date"]?["coming_soon"]?.Value<bool>() ?? false);
                string releaseStr = data["release_date"]?["date"]?.Value<string>() ?? "";
                bool isEarlyAccess = data["early_access"]?.Value<bool>() ?? false;

                // Pokus o parsovanie dátumu
                long releaseDateUnix = 0;
                string releaseDateDisplay;

                if (!isReleased)
                {
                    releaseDateDisplay = string.IsNullOrEmpty(releaseStr) ? "Coming Soon" : releaseStr;
                }
                else if (DateTime.TryParse(releaseStr, out var dt))
                {
                    releaseDateUnix = new DateTimeOffset(dt).ToUnixTimeSeconds();
                    releaseDateDisplay = dt.ToString("M/d/yyyy");
                }
                else
                {
                    releaseDateDisplay = releaseStr;
                }

                // ─── Platforms ────────────────────────────────────────────────────
                bool win = data["platforms"]?["windows"]?.Value<bool>() ?? true;
                bool mac = data["platforms"]?["mac"]?.Value<bool>() ?? false;

                // ─── Pricing ──────────────────────────────────────────────────────
                bool isFree = data["is_free"]?.Value<bool>() ?? false;
                string price = "N/A";
                string origPrice = "";
                int discount = 0;

                if (isFree)
                {
                    price = "Free";
                }
                else if (isReleased)
                {
                    var priceOverview = data["price_overview"];
                    if (priceOverview != null)
                    {
                        price = priceOverview["final_formatted"]?.Value<string>() ?? "N/A";
                        discount = priceOverview["discount_percent"]?.Value<int>() ?? 0;
                        if (discount > 0)
                            origPrice = priceOverview["initial_formatted"]?.Value<string>() ?? "";
                    }
                }

                return new
                {
                    AppId = appId,
                    Name = name,
                    HeaderImageUrl = data["header_image"]?.Value<string>()
                                         ?? $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                    Tags = tags,
                    ReviewDesc = reviewDesc,
                    ReviewCss = reviewCss,
                    ReleaseDateUnix = releaseDateUnix,
                    ReleaseDateDisplay = releaseDateDisplay,
                    IsReleased = isReleased,
                    IsEarlyAccess = isEarlyAccess,
                    DateAddedUnix = dateAdded,
                    PlatformWindows = win,
                    PlatformMac = mac,
                    Price = price,
                    OriginalPrice = origPrice,
                    DiscountPercent = discount,
                    IsFree = isFree
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] FetchAppDetails error {appId}: {ex.Message}");
                return null;
            }
        }


        private async Task<List<object>> FetchBatchAppDetailsAsync(
            List<int> appIds,
            Dictionary<int, long> dateAddedLookup)
        {
            var result = new List<object>();

            try
            {
                var joined = string.Join(",", appIds);
                var url = $"https://store.steampowered.com/api/appdetails?appids={joined}&cc=us&l=en";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);

                foreach (var appId in appIds)
                {
                    try
                    {
                        var data = json[appId.ToString()]?["data"];
                        if (data == null) continue;

                        string name = data["name"]?.Value<string>() ?? "";
                        if (HasAdultName(name) || HasExplicitContent(data)) continue;

                        // Tags — categories first, fallback to genres
                        var tags = (data["categories"] as JArray)?
                            .Select(c => c["description"]?.Value<string>() ?? "")
                            .Where(t => !string.IsNullOrEmpty(t))
                            .Take(5)
                            .ToList() ?? new List<string>();

                        if (!tags.Any())
                            tags = (data["genres"] as JArray)?
                                .Select(g => g["description"]?.Value<string>() ?? "")
                                .Where(t => !string.IsNullOrEmpty(t))
                                .Take(5)
                                .ToList() ?? new List<string>();

                        // Reviews
                        string reviewDesc = "", reviewCss = "";
                        var reviewData = data["reviews"]?.Value<string>() ?? "";

                        // Skús metacritic ako fallback
                        int meta = data["metacritic"]?["score"]?.Value<int>() ?? 0;
                        if (meta > 0)
                        {
                            (reviewDesc, reviewCss) = meta switch
                            {
                                >= 90 => ("Overwhelmingly Positive", "overwhelmingPositive"),
                                >= 75 => ("Very Positive", "positive"),
                                >= 60 => ("Mostly Positive", "positive"),
                                >= 40 => ("Mixed", "mixed"),
                                _ => ("Mostly Negative", "negative")
                            };
                        }

                        if (string.IsNullOrEmpty(reviewDesc))
                            reviewDesc = "No Reviews";

                        // Release date
                        bool isReleased = !(data["release_date"]?["coming_soon"]?.Value<bool>() ?? false);
                        string releaseStr = data["release_date"]?["date"]?.Value<string>() ?? "";
                        bool isEarlyAccess = data["early_access"]?.Value<bool>() ?? false;

                        long releaseDateUnix = 0;
                        string releaseDateDisplay;

                        if (!isReleased)
                        {
                            releaseDateDisplay = string.IsNullOrEmpty(releaseStr) ? "Coming Soon" : releaseStr;
                        }
                        else if (DateTime.TryParse(releaseStr, out var dt))
                        {
                            releaseDateUnix = new DateTimeOffset(dt).ToUnixTimeSeconds();
                            releaseDateDisplay = dt.ToString("M/d/yyyy");
                        }
                        else
                        {
                            releaseDateDisplay = releaseStr;
                        }

                        // Platforms
                        bool win = data["platforms"]?["windows"]?.Value<bool>() ?? true;
                        bool mac = data["platforms"]?["mac"]?.Value<bool>() ?? false;

                        // Pricing
                        bool isFree = data["is_free"]?.Value<bool>() ?? false;
                        string price = "N/A", origPrice = "";
                        int discount = 0;

                        if (isFree)
                        {
                            price = "Free";
                        }
                        else if (isReleased)
                        {
                            var po = data["price_overview"];
                            if (po != null)
                            {
                                price = po["final_formatted"]?.Value<string>() ?? "N/A";
                                discount = po["discount_percent"]?.Value<int>() ?? 0;
                                if (discount > 0)
                                    origPrice = po["initial_formatted"]?.Value<string>() ?? "";
                            }
                        }

                        long dateAdded = dateAddedLookup.TryGetValue(appId, out var da) ? da : 0;

                        result.Add(new
                        {
                            AppId = appId,
                            Name = name,
                            HeaderImageUrl = data["header_image"]?.Value<string>()
                                                 ?? $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                            Tags = tags,
                            ReviewDesc = reviewDesc,
                            ReviewCss = reviewCss,
                            ReleaseDateUnix = releaseDateUnix,
                            ReleaseDateDisplay = releaseDateDisplay,
                            IsReleased = isReleased,
                            IsEarlyAccess = isEarlyAccess,
                            DateAddedUnix = dateAdded,
                            PlatformWindows = win,
                            PlatformMac = mac,
                            Price = price,
                            OriginalPrice = origPrice,
                            DiscountPercent = discount,
                            IsFree = isFree
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Wishlist] Parse error {appId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wishlist] Batch error: {ex.Message}");
            }

            return result;
        }

        [HttpGet("wishlist/{steamId}/ids")]
        public async Task<IActionResult> GetWishlistIds(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?key={_steamApiKey}&steamid={steamId}";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);

                var items = json["response"]?["items"] as JArray;
                if (items == null || !items.Any())
                    return Ok(new { Items = new List<object>() });

                var result = items.Select(item => new
                {
                    AppId = item["appid"]?.Value<int>() ?? 0,
                    DateAdded = item["date_added"]?.Value<long>() ?? 0
                })
                .Where(x => x.AppId > 0)
                .ToList();

                return Ok(new { Items = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        // ─── WISHLIST REMOVE ──────────────────────────────────────────────────────────
        // Vyžaduje Steam session cookies — funguje len pre prihláseného usera
        // Ak zlyhá (private / nesprávny token), vráti 401

        [HttpPost("wishlist/remove/{steamId}/{appId}")]
        public async Task<IActionResult> RemoveFromWishlist(string steamId, int appId)
        {
            try
            {
                // sessionid cookie musí prísť od klienta v hlavičke X-Steam-Session
                var sessionId = Request.Headers["X-Steam-Session"].FirstOrDefault();
                if (string.IsNullOrEmpty(sessionId))
                    return Unauthorized(new { Success = false, Message = "No Steam session." });

                var content = new FormUrlEncodedContent(new[]
                {
            new KeyValuePair<string, string>("sessionid", sessionId),
            new KeyValuePair<string, string>("appid",     appId.ToString())
        });

                var req = new HttpRequestMessage(HttpMethod.Post,
                    $"https://store.steampowered.com/api/removefromwishlist")
                {
                    Content = content
                };
                req.Headers.Add("Cookie", $"sessionid={sessionId}");
                req.Headers.Add("Referer", $"https://store.steampowered.com/app/{appId}");

                var response = await _http.SendAsync(req);
                var body = JObject.Parse(await response.Content.ReadAsStringAsync());
                bool ok = body["success"]?.Value<bool>() ?? false;

                return ok
                    ? Ok(new { Success = true })
                    : StatusCode(401, new { Success = false, Message = "Steam remove failed — profile may be private." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = ex.Message });
            }
        }

        // ─── GAME DETAILS — s 18+ filtrom ─────────────────────────────────

        [HttpGet("game/{appId}")]
        public async Task<IActionResult> GetGameDetails(int appId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=us&l=en";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var gameData = json[appId.ToString()]?["data"];

                if (gameData == null)
                    return NotFound(new { Message = "Game not found." });

                string name = gameData["name"]?.Value<string>() ?? "";

                if (HasAdultName(name) || HasExplicitContent(gameData))
                    return StatusCode(451, new { Message = "Content filtered." });

                bool isFree = gameData["is_free"]?.Value<bool>() ?? false;
                string price = isFree ? "Free"
                    : gameData["price_overview"]?["final_formatted"]?.Value<string>() ?? "N/A";

                var genres = gameData["genres"]?
                    .Select(g => g["description"]?.Value<string>() ?? "")
                    .Where(g => !string.IsNullOrEmpty(g))
                    .ToList() ?? new List<string>();

                var screenshots = gameData["screenshots"]?
                    .Select(s => s["path_full"]?.Value<string>() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList() ?? new List<string>();

                int discountPercent = gameData["price_overview"]?["discount_percent"]?.Value<int>() ?? 0;
                string originalPrice = discountPercent > 0
                    ? gameData["price_overview"]?["initial_formatted"]?.Value<string>() ?? ""
                    : "";

                return Ok(new
                {
                    AppId = appId,
                    Name = name,
                    Description = gameData["short_description"]?.Value<string>() ?? "",
                    HeaderImageUrl = gameData["header_image"]?.Value<string>() ?? "",
                    Price = price,
                    OriginalPrice = originalPrice,
                    DiscountPercent = discountPercent,
                    Genres = genres,
                    Screenshots = screenshots,
                    SteamStoreUrl = $"https://store.steampowered.com/app/{appId}"
                });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── FEATURED — kurátorský zoznam + shuffle ───────────────────────
        // Curated popular games list + daily shuffle so it feels fresh
        // Fallback na specials ak kurátor zlyhá — fallback to specials if curated fails

        [HttpGet("store/featured")]
        public async Task<IActionResult> GetFeatured([FromQuery] int count = 12)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                // Denný shuffle — rovnaká seed každý deň = rovnaké poradie celý deň
                // Daily shuffle — same seed each day = same order all day
                var daySeed = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
                var rng = new Random(daySeed);
                var shuffled = CuratedAppIds.OrderBy(_ => rng.Next()).ToArray();

                // Vráť AppID zoznam — apka si sama načíta detaily
                // Return AppID list — app loads details itself
                var result = shuffled.Take(count).Select(id => new
                {
                    AppId = id,
                    HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{id}/header.jpg"
                }).ToList();

                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── NEW TRENDING ─────────────────────────────────────────────────

        [HttpGet("store/newtrending")]
        public async Task<IActionResult> GetNewTrending([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                if (page > 0) await Task.Delay(1000);

                var url = "https://store.steampowered.com/api/featuredcategories/?cc=us&l=en";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var items = json["new_releases"]?["items"];

                if (items == null) return Ok(new { Games = new List<object>() });

                return Ok(new { Games = GetPage(items, page, filterAdult: true) });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── TOP SELLERS ──────────────────────────────────────────────────

        [HttpGet("store/topsellers")]
        public async Task<IActionResult> GetTopSellers([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                if (page > 0) await Task.Delay(1000);

                var url = "https://store.steampowered.com/api/featuredcategories/?cc=us&l=en";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var items = json["top_sellers"]?["items"];

                if (items == null) return Ok(new { Games = new List<object>() });

                return Ok(new { Games = GetPage(items, page, filterAdult: true) });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── SPECIALS ─────────────────────────────────────────────────────

        [HttpGet("store/specials")]
        public async Task<IActionResult> GetSpecials([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                if (page > 0) await Task.Delay(1000);

                var url = "https://store.steampowered.com/api/featuredcategories/?cc=us&l=en";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var items = json["specials"]?["items"];

                if (items == null) return Ok(new { Games = new List<object>() });

                return Ok(new { Games = GetPage(items, page, filterAdult: true) });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── HELPER ───────────────────────────────────────────────────────

        private List<object> GetPage(JToken items, int page, bool filterAdult = false)
        {
            var result = new List<object>();
            int skip = page * 10;
            int count = 0;

            foreach (var item in items)
            {
                string name = item["name"]?.Value<string>() ?? "";
                if (filterAdult && HasAdultName(name)) continue;

                if (count < skip) { count++; continue; }
                if (result.Count >= 10) break;

                int finalPrice = item["final_price"]?.Value<int>() ?? 0;
                int discount = item["discount_percent"]?.Value<int>() ?? 0;

                result.Add(new
                {
                    AppId = item["id"]?.Value<int>() ?? 0,
                    Name = name,
                    HeaderImageUrl = item["header_image"]?.Value<string>() ?? "",
                    Price = finalPrice == 0 ? "Free" : $"${finalPrice / 100.0:F2}",
                    DiscountPercent = discount
                });

                count++;
            }

            return result;
        }
    }
}