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

        // Rýchly name-based prefilter — slová ktoré jednoznačne signalizujú 18+ obsah
        // Quick name-based prefilter — words that clearly signal adult content
        private static readonly string[] AdultKeywords = new[]
        {
            "hentai", "nude", "naked", "erotic", "xxx", "porn", "sex", "lewd",
            "nsfw", "18+", "adult", "fuck", "futa", "cumming", "horny"
        };

        private static bool HasAdultName(string name)
        {
            var lower = name.ToLowerInvariant();
            return AdultKeywords.Any(k => lower.Contains(k));
        }

        // Správny 18+ check cez content_descriptors — len sexual/nudity, nie violence
        // Proper 18+ check via content_descriptors — sexual/nudity only, not violence
        // ID 1 = Some Nudity or Sexual Content
        // ID 3 = Adult Only Sexual Content  
        // ID 4 = Frequent Nudity or Sexual Content
        // ID 2 = Frequent Violence (CoD) — prechádza / passes
        // ID 5 = General Mature Content (CoD) — prechádza / passes
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

                // Meno check — rýchly filter pred content_descriptors
                // Name check — quick filter before content_descriptors
                string name = gameData["name"]?.Value<string>() ?? "";
                if (HasAdultName(name))
                    return StatusCode(451, new { Message = "Content filtered." });

                // Content descriptors check
                if (HasExplicitContent(gameData))
                    return StatusCode(451, new { Message = "Content filtered." });

                bool isFree = gameData["is_free"]?.Value<bool>() ?? false;
                string price = isFree ? "Free" : gameData["price_overview"]?["final_formatted"]?.Value<string>() ?? "N/A";

                var genres = gameData["genres"]?
                    .Select(g => g["description"]?.Value<string>() ?? "")
                    .Where(g => !string.IsNullOrEmpty(g))
                    .ToList() ?? new List<string>();

                var screenshots = gameData["screenshots"]?
                    .Select(s => s["path_full"]?.Value<string>() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList() ?? new List<string>();

                return Ok(new
                {
                    AppId = appId,
                    Name = name,
                    Description = gameData["short_description"]?.Value<string>() ?? "",
                    HeaderImageUrl = gameData["header_image"]?.Value<string>() ?? "",
                    Price = price,
                    Genres = genres,
                    Screenshots = screenshots,
                    SteamStoreUrl = $"https://store.steampowered.com/app/{appId}"
                });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── FEATURED — používa specials sekciu ───────────────────────────
        // Uses specials section — contains well-known games like Cyberpunk, GTA V, Rust
        // Filtruje 18+ cez meno — name-based 18+ filter

        [HttpGet("store/featured")]
        public async Task<IActionResult> GetFeatured()
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var url = "https://store.steampowered.com/api/featuredcategories/?cc=us&l=en";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);

                // specials = Featured & Recommended hry — zľavy na známe hry
                // specials = Featured & Recommended games — discounts on well-known games
                var items = json["specials"]?["items"];

                if (items == null) return Ok(new { Games = new List<object>() });

                var result = new List<object>();
                foreach (var item in items)
                {
                    string name = item["name"]?.Value<string>() ?? "";

                    // Preskočiť 18+ podľa mena — skip adult content by name
                    if (HasAdultName(name)) continue;

                    int finalPrice = item["final_price"]?.Value<int>() ?? 0;
                    int origPrice = item["original_price"]?.Value<int>() ?? 0;
                    int discount = item["discount_percent"]?.Value<int>() ?? 0;

                    string price = finalPrice == 0 ? "Free"
                        : $"${finalPrice / 100.0:F2}";
                    string originalPriceStr = origPrice == 0 ? "" : $"${origPrice / 100.0:F2}";

                    result.Add(new
                    {
                        AppId = item["id"]?.Value<int>() ?? 0,
                        Name = name,
                        HeaderImageUrl = item["header_image"]?.Value<string>() ?? "",
                        Price = price,
                        OriginalPrice = originalPriceStr,
                        DiscountPercent = discount
                    });
                }

                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── NEW TRENDING — s name filtrom ────────────────────────────────

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

                // Preskočiť 18+ — skip adult
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