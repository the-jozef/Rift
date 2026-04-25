using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SteamProxyBackend.Controllers
{
    [ApiController]
    [Route("api/steam")]
    public class SteamController : ControllerBase
    {
        private readonly HttpClient _http;
        private readonly string _steamApiKey;

        // Rate limiting — obmedzenie požiadaviek per IP
        private static readonly ConcurrentDictionary<string, DateTime> _lastRequestTime = new();
        private const int RateLimitSeconds = 2;

        public SteamController(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _http = httpFactory.CreateClient();
            _steamApiKey = config["SteamApiKey"]
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

        // ─── PLAYER SUMMARY ───────────────────────────────────────────────────

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
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        // ─── OWNED GAMES / LIBRARY ────────────────────────────────────────────

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
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        // ─── WISHLIST ─────────────────────────────────────────────────────────

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
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        // ─── GAME DETAILS ─────────────────────────────────────────────────────

        [HttpGet("game/{appId}")]
        public async Task<IActionResult> GetGameDetails(int appId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=us&l=en";
                var response = await _http.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                var gameData = data?[appId.ToString()]?.data;

                if (gameData == null)
                    return NotFound(new { Message = "Game not found." });

                bool isFree = (bool)(gameData.is_free ?? false);
                string price = isFree ? "Free" : (string)(gameData.price_overview?.final_formatted ?? "N/A");

                var genres = new List<string>();
                if (gameData.genres != null)
                    foreach (var g in gameData.genres)
                        genres.Add((string)g.description);

                var screenshots = new List<string>();
                if (gameData.screenshots != null)
                    foreach (var s in gameData.screenshots)
                        screenshots.Add((string)s.path_full);

                return Ok(new
                {
                    AppId = appId,
                    Name = (string)gameData.name,
                    Description = (string)gameData.short_description,
                    HeaderImageUrl = (string)gameData.header_image,
                    Price = price,
                    Genres = genres,
                    Screenshots = screenshots,
                    SteamStoreUrl = $"https://store.steampowered.com/app/{appId}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        // ─── STORE CATEGORIES ─────────────────────────────────────────────────

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
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                var items = data?.new_releases?.items;

                if (items == null) return Ok(new { Games = new List<object>() });

                return Ok(new { Games = GetPage(items, page) });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

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
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                var items = data?.top_sellers?.items;

                if (items == null) return Ok(new { Games = new List<object>() });

                return Ok(new { Games = GetPage(items, page) });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

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
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                var items = data?.specials?.items;

                if (items == null) return Ok(new { Games = new List<object>() });

                return Ok(new { Games = GetPage(items, page) });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── HELPER ───────────────────────────────────────────────────────────

        private List<object> GetPage(dynamic items, int page)
        {
            var result = new List<object>();
            int skip = page * 10;
            int count = 0;

            foreach (var item in items)
            {
                if (count < skip) { count++; continue; }
                if (result.Count >= 10) break;

                result.Add(new
                {
                    AppId = (int)item.id,
                    Name = (string)item.name,
                    HeaderImageUrl = (string)item.header_image,
                    Price = (string)(item.final_price == 0 ? "Free" : item.final_formatted ?? "N/A"),
                    DiscountPercent = (int)(item.discount_percent ?? 0)
                });

                count++;
            }

            return result;
        }
    }
}