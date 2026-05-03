using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

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

        // ─── WISHLIST ─────────────────────────────────────────────────────

        [HttpGet("wishlist/{steamId}")]
        public async Task<IActionResult> GetWishlist(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?key={_steamApiKey}&steamid={steamId}";
                var response = await _http.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                var items = data?.response?.items;

                if (items == null) return Ok(new { Games = new List<object>() });

                var result = new List<object>();
                foreach (var item in items)
                {
                    int appId = (int)item.appid;
                    result.Add(new
                    {
                        AppId = appId,
                        HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
                    });
                }

                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
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