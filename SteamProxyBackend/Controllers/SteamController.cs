using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json.Serialization;
using static System.Net.WebRequestMethods;


namespace SteamProxyBackend.Controllers
{
    [ApiController]
    [Route("api/steam")]
    public class SteamController : ControllerBase
    {
        private readonly HttpClient _http;
        private readonly string _steamApiKey;

        // ─── RATE LIMIT ───────────────────────────────────────────────────
        private static readonly ConcurrentDictionary<string, DateTime> _lastRequestTime = new();
        private const int RateLimitSeconds = 2;

        // ─── GAME DETAIL CACHE ─────────────────────
        // Prevents repeated Steam API calls for the same game
        private static readonly ConcurrentDictionary<int, (StoreGameDto Data, DateTime At)> _detailCache = new();
        private static readonly TimeSpan DetailCacheTTL = TimeSpan.FromHours(6);

        // ─── CURATED APPIDS — ONLY WELL-KNOWN GAMES ───────────────────────
        private static readonly int[] CuratedAppIds =
        {
            730,     570,     578080,  1172470, 1086940, 271590,  1938090, 1091500, 1245620, 1623730,
            252490,  1548130, 1145360, 1174180, 413150,  105600,  292030,  236390,  230410,  381210,
            4000,    550,     227300,  289070,  394360,  107410,  990080,  255710,  346110,  1888160,
            2308810, 2915570, 1281410, 1326340, 1966720, 2215430, 1612100, 1151340, 2483980, 1675970,
            2357570, 2050650, 2023140, 1794680, 1326470, 1551360, 427520,  526870,  322170,  304930,
            440,     620,     400,     70,      220,     489830,  377160,  250900,  367520,  582010,
            646570,  1307580, 1361510, 1593500, 1817070, 1250410, 814380,  1158310, 435150,  242760,
            1063730, 1046930, 306130,  1238810, 1238840, 311210,  221380,  519860,  205100,  10180,
            284160,  949230,  261550,  1240440, 221100,  359550,  1942660, 2246340, 2540090, 1326410,
            644830,  508440,  49520,   391540,  268910,  1446780, 218620,  239140,  1426210, 1281930
        };

        // ─── 18+ FILTER ───────────────────────────────────────────────────
        private static readonly string[] AdultKeywords =
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
 
        // ─── All fields the ViewModel needs ──────────────────────────────────────────────────────────────
        public class StoreGameDto
        {
            public int AppId { get; set; }
            public string Name { get; set; } = "";
            public string HeaderImageUrl { get; set; } = "";
            public List<string> Screenshots { get; set; } = new();
            public List<string> Tags { get; set; } = new();
            public List<string> FeaturedTags { get; set; } = new();
            public List<string> Genres { get; set; } = new();
            public string Price { get; set; } = "N/A";
            public string OriginalPrice { get; set; } = "";
            public int DiscountPercent { get; set; }
            public bool IsFree { get; set; }
            public bool HasDiscount { get; set; }
            public string StatusText { get; set; } = "Available Now";
            public bool IsRecommended { get; set; }
            public string Description { get; set; } = "";
            public string SteamStoreUrl { get; set; } = "";
        }

        // ─── Fetches game ─────────────────────────────────────────────────────────────────────────────────
        private async Task<StoreGameDto?> FetchSingleForStoreAsync(int appId)
        {
            // 1.memory cache
            if (_detailCache.TryGetValue(appId, out var cached) &&
                DateTime.UtcNow - cached.At < DetailCacheTTL)
                return cached.Data;

            try
            {           
                var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=sk&l=en";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var entry = json[appId.ToString()];

                if (entry?["success"]?.Value<bool>() != true) return null;
                var data = entry["data"];
                if (data == null) return null;

                string name = data["name"]?.Value<string>() ?? "";
                if (string.IsNullOrEmpty(name)) return null;
                if (HasAdultName(name) || HasExplicitContent(data)) return null;

                // PRICE — use Steam's formatted string
                bool isFree = data["is_free"]?.Value<bool>() ?? false;
                string price = "N/A";
                string origPrice = "";
                int discount = 0;

                if (isFree)
                {
                    price = "Free To Play";
                }
                else
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

                // ─── TAGS ─────────────────────────────────────────────────
                var genres = (data["genres"] as JArray)?
                    .Select(g => g["description"]?.Value<string>() ?? "")
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Take(3).ToList() ?? new List<string>();

                var keyCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Single-player", "Multi-player", "Co-op", "Online Co-op",
                    "PvP", "Online PvP", "Early Access", "Co-op Campaign",
                    "Cross-Platform Multiplayer"
                };
                var catTags = (data["categories"] as JArray)?
                    .Select(c => c["description"]?.Value<string>() ?? "")
                    .Where(t => keyCats.Contains(t))
                    .Take(2).ToList() ?? new List<string>();

                var tags = genres.Concat(catTags).Distinct().Take(4).ToList();

                // ─── SCREENSHOTS ─────────────────────────────────────────
                var screenshots = (data["screenshots"] as JArray)?
                    .Select(s => s["path_full"]?.Value<string>() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Take(4).ToList() ?? new List<string>();

                var dto = new StoreGameDto
                {
                    AppId = appId,
                    Name = name,
                    HeaderImageUrl = data["header_image"]?.Value<string>()
                        ?? $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                    Screenshots = screenshots,
                    Tags = tags,
                    FeaturedTags = tags,
                    Genres = genres,
                    Price = price,
                    OriginalPrice = origPrice,
                    DiscountPercent = discount,
                    IsFree = isFree,
                    HasDiscount = discount > 0,
                    StatusText = "Available Now",
                    IsRecommended = false,
                    Description = data["short_description"]?.Value<string>() ?? "",
                    SteamStoreUrl = $"https://store.steampowered.com/app/{appId}"
                };

                // 3. Save to memory
                _detailCache[appId] = (dto, DateTime.UtcNow);
                return dto;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Store] Fetch {appId}: {ex.Message}");
                return null;
            }
        }

        // ─── 5 games per page ──────────────────────────────────────────────────────────────
        private async Task<List<StoreGameDto>> GetCuratedSectionAsync(int sectionSeed,int page,int size = 5,bool discountedOnly = false)
        {
            // Same shuffle all day for given seed
            var rng = new Random(sectionSeed);
            var shuffled = CuratedAppIds.Distinct().OrderBy(_ => rng.Next()).ToArray();

            var result = new List<StoreGameDto>();
            int skipTarget = page * size;
            int skipped = 0;

            foreach (var id in shuffled)
            {
                if (result.Count >= size) break;

                bool isCached = _detailCache.TryGetValue(id, out var ce) &&
                                DateTime.UtcNow - ce.At < DetailCacheTTL;

                var dto = await FetchSingleForStoreAsync(id);
                if (dto == null) continue;

                // Delay only when actually fetching
                if (!isCached)
                    await Task.Delay(150);

                if (discountedOnly && !dto.HasDiscount && !dto.IsFree)
                    continue;

                if (skipped < skipTarget) { skipped++; continue; }

                result.Add(dto);
            }

            return result;
        }

        // ──── STORE ENDPOINTS ─────────────────────────────────────────────────────────────

        [HttpGet("store/featured")]
        public async Task<IActionResult> GetFeatured([FromQuery] int page = 0, [FromQuery] int count = 5)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var daySeed = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
                var games = await GetCuratedSectionAsync(daySeed, page, Math.Min(count, 5));
                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── NEW TRENDING ─────────────────────────────────────────────────────────────────────────
        [HttpGet("store/newtrending")]
        public async Task<IActionResult> GetNewTrending([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var daySeed = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd")) + 1000;
                var games = await GetCuratedSectionAsync(daySeed, page, 5);
                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── TOP SELLERS ─────────────────────────────────────────────────────────────────────────
        [HttpGet("store/topsellers")]
        public async Task<IActionResult> GetTopSellers([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var daySeed = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd")) + 2000;
                var games = await GetCuratedSectionAsync(daySeed, page, 5);
                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── SPECIALS GAMES ─────────────────────────────────────────────────────────────────────────
        [HttpGet("store/specials")]
        public async Task<IActionResult> GetSpecials([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var daySeed = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd")) + 3000;

                var games = await GetCuratedSectionAsync(daySeed, page, 5, discountedOnly: true);

                if (games.Count < 5)
                {
                    var pad = await GetCuratedSectionAsync(daySeed + 500, page, 5 - games.Count);
                    // Odfiltruj duplicity
                    var existingIds = new HashSet<int>(games.Select(g => g.AppId));
                    games.AddRange(pad.Where(g => !existingIds.Contains(g.AppId)));
                }

                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── GAME DETAILS ──────────────────────────────────────────────────────────────
        [HttpGet("game/{appId}")]
        public async Task<IActionResult> GetGameDetails(int appId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                var dto = await FetchSingleForStoreAsync(appId);
                if (dto == null)
                    return NotFound(new { Message = "Game not found or filtered." });

                return Ok(dto);
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── PLAYER SUMMARY ──────────────────────────────────────────────────────────────
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

        // ─── OWNED GAMES / LIBRARY ──────────────────────────────────────────────────────────────
        [HttpGet("library/{steamId}")]
        public async Task<IActionResult> GetOwnedGames(string steamId)
        {
            try
            {
                var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/" +
                          $"?key={_steamApiKey}&steamid={steamId}&include_appinfo=true" +
                          $"&include_played_free_games=true&skip_unvetted_apps=false&include_free_sub=1";

                var response = await _http.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                var games = data?.response?.games;

                if (games == null) return Ok(new { Games = new List<object>() });

                var result = new List<object>();
                foreach (var game in games)
                {
                    string name = (string)(game.name ?? "");
                    if (string.IsNullOrEmpty(name)) continue;
                    result.Add(new
                    {
                        AppId = (int)game.appid,
                        Name = name,
                        PlaytimeMinutes = (int)(game.playtime_forever ?? 0),
                        IconUrl = $"https://media.steampowered.com/steamcommunity/public/images/apps/{game.appid}/{game.img_icon_url}.jpg",
                        HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{game.appid}/header.jpg"
                    });
                }
                Debug.WriteLine($"[Library] {result.Count} games for {steamId}");
                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ─── FULL LIBRARY ──────────────────────────────────────────────────────────────
        [HttpGet("library/{steamId}/full")]
        public async Task<IActionResult> GetFullLibrary(string steamId)
        {
            try
            {
                var url = $"https://steamcommunity.com/profiles/{steamId}/games/?tab=all&xml=1";
                var response = await _http.GetStringAsync(url);

                if (response.Contains("profile is private") || response.Contains("privacyMessage"))
                    return Ok(new { Games = new List<object>(), Error = "Profile is private" });

                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(response);
                var games = doc.SelectNodes("//game");
                if (games == null || games.Count == 0) return Ok(new { Games = new List<object>() });

                var result = new List<object>();
                foreach (System.Xml.XmlNode game in games)
                {
                    var appIdStr = game["appID"]?.InnerText;
                    var name = game["name"]?.InnerText;
                    if (!int.TryParse(appIdStr, out int appId) || appId <= 0) continue;
                    if (string.IsNullOrEmpty(name)) continue;

                    int.TryParse(game["hoursOnRecord"]?.InnerText?.Replace(",", "").Replace(".", "") ?? "0", out int hoursRaw);

                    result.Add(new
                    {
                        AppId = appId,
                        Name = name,
                        PlaytimeMinutes = hoursRaw,
                        IconUrl = $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/capsule_sm_120.jpg",
                        HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
                    });
                }
                Debug.WriteLine($"[Library] Full library: {result.Count} games");
                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ──── ACHIEVEMENTS ─────────────────────────────────────────────────────────────
        [HttpGet("achievements/{appId}/{steamId}")]
        public async Task<IActionResult> GetAchievements(int appId, string steamId)
        {
            try
            {
                var schemaUrl = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={_steamApiKey}&appid={appId}&l=en";
                var schemaJson = await _http.GetStringAsync(schemaUrl);
                var schemaData = JObject.Parse(schemaJson);
                var schemaAchs = schemaData["game"]?["availableGameStats"]?["achievements"];

                var iconLookup = new Dictionary<string, (string icon, string iconGray)>();
                if (schemaAchs != null)
                    foreach (var a in schemaAchs)
                    {
                        var name = a["name"]?.Value<string>() ?? "";
                        iconLookup[name] = (a["icon"]?.Value<string>() ?? "", a["icongray"]?.Value<string>() ?? "");
                    }

                var percentLookup = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var pctUrl = $"https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v0002/?gameid={appId}";
                    var pctJson = await _http.GetStringAsync(pctUrl);
                    var pctData = JObject.Parse(pctJson);
                    var pctAchs = pctData["achievementpercentages"]?["achievements"];
                    if (pctAchs != null)
                        foreach (var a in pctAchs)
                        {
                            var name = a["name"]?.Value<string>() ?? "";
                            var pct = a["percent"]?.Value<double>() ?? 100.0;
                            if (!string.IsNullOrEmpty(name)) percentLookup[name] = pct;
                        }
                }
                catch { }

                var statsUrl = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={_steamApiKey}&appid={appId}&steamid={steamId}&l=en";
                var result = new List<object>();
                int unlocked = 0;

                try
                {
                    var statsJson = await _http.GetStringAsync(statsUrl);
                    var statsData = JObject.Parse(statsJson);
                    var playerAchs = statsData["playerstats"]?["achievements"];

                    if (playerAchs != null)
                        foreach (var a in playerAchs)
                        {
                            var apiName = a["apiname"]?.Value<string>() ?? "";
                            var achieved = a["achieved"]?.Value<int>() == 1;
                            var unlockTime = a["unlocktime"]?.Value<long>() ?? 0;
                            if (achieved) unlocked++;
                            iconLookup.TryGetValue(apiName, out var icons);
                            percentLookup.TryGetValue(apiName, out var rarity);
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
                                    : (DateTime?)null,
                                RarityPercentage = rarity
                            });
                        }
                }
                catch
                {
                    foreach (var kvp in iconLookup)
                    {
                        percentLookup.TryGetValue(kvp.Key, out var rarity);
                        result.Add(new
                        {
                            ApiName = kvp.Key,
                            Name = "",
                            Description = "",
                            IconUrl = kvp.Value.icon,
                            IconGrayUrl = kvp.Value.iconGray,
                            Unlocked = false,
                            UnlockTime = (DateTime?)null,
                            RarityPercentage = rarity
                        });
                    }
                }

                return Ok(new { Achievements = result, Total = result.Count, Unlocked = unlocked });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
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

        // ──── PLAYER STATS ──────────────────────────────────────────────────────────────
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
            // Private profile
            catch { return Ok(new { Achievements = new List<object>() }); }
        }

        // ──── WISHLIST ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
        [HttpGet("wishlist/{steamId}")]
        public async Task<IActionResult> GetWishlist(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests. Please wait." });

                // ─── Získaj ID zoznam ───────────────────────────────────────────
                var listUrl = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?key={_steamApiKey}&steamid={steamId}";
                var listJson = JObject.Parse(await _http.GetStringAsync(listUrl));
                var items = listJson["response"]?["items"] as JArray;

                if (items == null || !items.Any())
                    return Ok(new { Games = new List<object>() });

                var dateAddedLookup = items
                    .Where(i => (i["appid"]?.Value<int>() ?? 0) > 0)
                    .ToDictionary(i => i["appid"]!.Value<int>(), i => i["date_added"]?.Value<long>() ?? 0);

                var result = new List<object>();
                int batchSize = 10;
                var appIds = dateAddedLookup.Keys.ToList();

                for (int i = 0; i < appIds.Count; i += batchSize)
                {
                    var batch = appIds.Skip(i).Take(batchSize).ToList();
                    var batchResults = await FetchBatchAppDetailsAsync(batch, dateAddedLookup);
                    result.AddRange(batchResults);
                    if (i + batchSize < appIds.Count) await Task.Delay(500);
                }

                result = result.Cast<dynamic>()
                    .OrderBy(g => !(bool)g.IsReleased)
                    .ThenByDescending(g => (long)g.DateAddedUnix)
                    .Cast<object>().ToList();

                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
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

                var result = items
                    .Select(item => new { AppId = item["appid"]?.Value<int>() ?? 0, DateAdded = item["date_added"]?.Value<long>() ?? 0 })
                    .Where(x => x.AppId > 0).ToList();

                return Ok(new { Items = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }
                
        [HttpGet("wishlist/detail/{appId}")]
        public async Task<IActionResult> GetWishlistGameDetail(int appId)
        {
            try
            {
                var result = await FetchAppDetailsAsync(appId, 0);
                if (result == null)
                    return NotFound(new { Message = "Game not found." });

                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ──── WISHLIST REMOVE ──────────────────────────────────────────────────────────────
        [HttpPost("wishlist/remove/{steamId}/{appId}")]
        public async Task<IActionResult> RemoveFromWishlist(string steamId, int appId)
        {
            try
            {
                var sessionId = Request.Headers["X-Steam-Session"].FirstOrDefault();
                if (string.IsNullOrEmpty(sessionId))
                    return Unauthorized(new { Success = false, Message = "No Steam session." });

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("sessionid", sessionId),
                    new KeyValuePair<string, string>("appid", appId.ToString())
                });

                var req = new HttpRequestMessage(HttpMethod.Post, "https://store.steampowered.com/api/removefromwishlist")
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
                    : StatusCode(401, new { Success = false, Message = "Steam remove failed." });
            }
            catch (Exception ex) { return StatusCode(500, new { Success = false, Message = ex.Message }); }
        }

        [HttpPost("wishlist/batch")]
        public async Task<IActionResult> GetWishlistBatch([FromBody] WishlistBatchRequest request)
        {
            try
            {
                if (request?.AppIds == null || !request.AppIds.Any())
                    return Ok(new { Games = new List<object>() });

                var dummy = request.AppIds.ToDictionary(id => id, _ => 0L);
                var result = await FetchBatchAppDetailsAsync(request.AppIds, dummy);

                Console.WriteLine($"[Batch] Returned {result.Count} / {request.AppIds.Count} games");
                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ────  WISHLIST HELPERS ─────────────────────────────────────────────────────────────
        private async Task<object?> FetchAppDetailsAsync(int appId, long dateAdded)
        {
            try
            {
                var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=sk&l=en";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);

                var data = json[appId.ToString()]?["data"];
                if (data == null) return null;

                string name = data["name"]?.Value<string>() ?? "";

                if (HasAdultName(name) || HasExplicitContent(data)) return null;

                // ─── Tags ─────────────────────────────────────────────────────────
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
                            >= 95 => ("Very Positive", "verypositive"),
                            >= 80 => ("Positive", "positive"),
                            >= 70 => ("Mostly Positive", "mostlypositive"),
                            >= 40 => ("Mixed", "mixed"),
                            >= 20 => ("Mostly Negative", "mostlynegative"),
                            _ => ("Negative", "negative")
                        };
                    }
                }

                // ─── Release date ──────────────────────────────────────────────────
                bool isReleased = !(data["release_date"]?["coming_soon"]?.Value<bool>() ?? false);
                string releaseStr = data["release_date"]?["date"]?.Value<string>() ?? "";
                long releaseDateUnix = 0;
                string releaseDateDisplay;

                if (!isReleased)
                    releaseDateDisplay = string.IsNullOrEmpty(releaseStr) ? "Coming Soon" : releaseStr;
                else if (DateTime.TryParse(releaseStr, out var dt))
                {
                    releaseDateUnix = new DateTimeOffset(dt).ToUnixTimeSeconds();
                    releaseDateDisplay = dt.ToString("M/d/yyyy");
                }
                else
                    releaseDateDisplay = releaseStr;

                // ─── Platforms ────────────────────────────────────────────────────
                bool win = data["platforms"]?["windows"]?.Value<bool>() ?? true;
                bool mac = data["platforms"]?["mac"]?.Value<bool>() ?? false;

                // ─── Pricing ──────────────────────────────────────────────────────
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
                        if (discount > 0) origPrice = po["initial_formatted"]?.Value<string>() ?? "";
                    }
                }

                return new
                {
                    AppId = appId,
                    Name = name,
                    HeaderImageUrl = data["header_image"]?.Value<string>()
                        ?? $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                    Tags = tags,
                    ReleaseDateUnix = releaseDateUnix,
                    ReleaseDateDisplay = releaseDateDisplay,
                    IsReleased = isReleased,
                    DateAddedUnix = dateAdded,
                    PlatformWindows = data["platforms"]?["windows"]?.Value<bool>() ?? true,
                    PlatformMac = data["platforms"]?["mac"]?.Value<bool>() ?? false,
                    Price = price,
                    OriginalPrice = origPrice,
                    DiscountPercent = discount,
                    IsFree = isFree
                };
            }
            catch (Exception ex) { 
                Debug.WriteLine($"[Wishlist] FetchAppDetails {appId}: {ex.Message}");
                return null; }
        }

        private async Task<(string desc, string css)> FetchReviewsAsync(int appId)
        {
            try
            {
                var url = $"https://store.steampowered.com/appreviews/{appId}?json=1&language=all&review_type=all&purchase_type=all&num_per_page=0";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);

                var summary = json["query_summary"];
                if (summary == null) { 
                    Console.WriteLine($"[Reviews] {appId}: query_summary is null");
                    return ("No Reviews", ""); }
            
 
                int total = summary["total_reviews"]?.Value<int>() ?? 0;
                if (total == 0) return ("No Reviews", "");

                var descFromSummary = summary["review_score_desc"]?.Value<string>() ?? "";
                if (!string.IsNullOrEmpty(descFromSummary) && descFromSummary != "No user reviews")
                {
                    return descFromSummary switch
                    {
                        "Very Positive" => ("Very Positive", "veryPositive"),
                        "Positive" => ("Positive", "positive"),
                        "Mostly Positive" => ("Mostly Positive", "mostlyPositive"),
                        "Mixed" => ("Mixed", "mixed"),
                        "Mostly Negative" => ("Mostly Negative", "mostlyNegative"),
                        "Very Negative" => ("Very Negative", "veryNegative"),
                        _ => ("No Reviews", "")
                    };
                }

                int pos = summary["total_positive"]?.Value<int>() ?? 0;

                Console.WriteLine($"[Reviews] {appId}: pos={pos}, total={total}");

                double pct = (double)pos / total * 100;
                return pct switch
                {
                    >= 90 => ("Very Positive", "veryPositive"),
                    >= 75 => ("Positive", "positive"),
                    >= 60 => ("Mostly Positive", "mostlyPositive"),
                    >= 40 => ("Mixed", "mixed"),
                    >= 20 => ("Mostly Negative", "mostlyNegative"),
                    _ => ("Negative", "negative"),
                };
            }
            catch {return ("No Reviews", ""); }
        }
       
        private async Task<List<object>> FetchBatchAppDetailsAsync(List<int> appIds, Dictionary<int, long> dateAddedLookup)
        {
            var result = new List<object>();

            foreach (var appId in appIds)
            {
                try
                {
                    var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=sk&l=en";
                    var response = await _http.GetStringAsync(url);
                    var json = JObject.Parse(response);

                    var entry = json[appId.ToString()];
                    if (entry?["success"]?.Value<bool>() != true) continue;

                    var data = entry["data"];
                    Console.WriteLine($"[R] {appId} score={data["review_score"]} desc={data["review_score_desc"]} type={data["type"]}");
                    if (data == null) continue;

                    string name = data["name"]?.Value<string>() ?? "";
                    if (HasAdultName(name) || HasExplicitContent(data)) continue;

                    var popularCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Single-player", "Multi-player", "Co-op", "Online Co-op",
                        "Tactical", "PvP", "Online PvP", "Co-op Campaign"
                    };

                    var categoryTags = (data["categories"] as JArray)?
                        .Select(c => c["description"]?.Value<string>() ?? "")
                        .Where(t => !string.IsNullOrEmpty(t) && popularCategories.Contains(t))
                        .Take(2).ToList() ?? new List<string>();

                    var genreTags = (data["genres"] as JArray)?
                        .Select(g => g["description"]?.Value<string>() ?? "")
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Take(3).ToList() ?? new List<string>();

                    var tags = categoryTags.Concat(genreTags).Distinct().Take(4).ToList();
                    if (!tags.Any()) tags = new List<string> { "Game" };

                    string reviewDesc = "", reviewCss = "";

                    int reviewScore = data["review_score"]?.Value<int>() ?? -1;
                    if (reviewScore > 0)
                        (reviewDesc, reviewCss) = reviewScore switch
                        {
                            9 => ("Very Positive", "veryPositive"),
                            8 => ("Positive", "positive"),
                            7 => ("Mostly Positive", "mostlyPositive"),
                            5 => ("Mixed", "mixed"),
                            4 => ("Mostly Negative", "mostlyNegative"),
                            3 => ("Negative", "negative"),
                            1 => ("Very Negative", "veryNegative"),
                            _ => ("", "")
                        };

                    if (string.IsNullOrEmpty(reviewDesc))
                    {
                        var scoreDesc = data["review_score_desc"]?.Value<string>() ?? "";
                        if (!string.IsNullOrEmpty(scoreDesc) && scoreDesc != "No user reviews")
                            (reviewDesc, reviewCss) = scoreDesc switch
                            {
                                "Very Positive" => ("Very Positive", "veryPositive"),
                                "Positive" => ("Positive", "positive"),
                                "Mostly Positive" => ("Mostly Positive", "mostlyPositive"),
                                "Mixed" => ("Mixed", "mixed"),
                                "Mostly Negative" => ("Mostly Negative", "mostlyNegative"),
                                "Very Negative" => ("Very Negative", "veryNegative"),
                                _ => ("", "")
                            };
                    }

                    if (string.IsNullOrEmpty(reviewDesc))
                    {
                        int meta = data["metacritic"]?["score"]?.Value<int>() ?? 0;
                        if (meta > 0)
                        {
                            (reviewDesc, reviewCss) = meta switch
                            {
                                >= 90 => ("Very Positive", "veryPositive"),
                                >= 75 => ("Positive", "positive"),
                                >= 60 => ("Mostly Positive", "mostlyPositive"),
                                >= 40 => ("Mixed", "mixed"),
                                >= 20 => ("Mostly Negative", "mostlyNegative"),
                                > 0 => ("Negative", "negative"),
                                _ => ("No Reviews", "")
                            };
                        }
                    }

                    if (string.IsNullOrEmpty(reviewDesc))
                        reviewDesc = "No Reviews";

                    bool isDlc = data["type"]?.Value<string>()?.ToLower() == "dlc";
                    if (string.IsNullOrEmpty(reviewDesc) || isDlc)
                        (reviewDesc, reviewCss) = await FetchReviewsAsync(appId);

                    bool isReleased = !(data["release_date"]?["coming_soon"]?.Value<bool>() ?? false);
                    bool isPreOrder = !isReleased && data["price_overview"] != null;
                    string releaseStr = data["release_date"]?["date"]?.Value<string>() ?? "";

                    long releaseDateUnix = 0;
                    string releaseDateDisplay;

                    if (!isReleased)
                        releaseDateDisplay = string.IsNullOrEmpty(releaseStr) ? "Coming Soon" : releaseStr;
                    else if (DateTime.TryParse(releaseStr, out var dt))
                    {
                        releaseDateUnix = new DateTimeOffset(dt).ToUnixTimeSeconds();
                        releaseDateDisplay = dt.ToString("M/d/yyyy");
                    }
                    else
                        releaseDateDisplay = releaseStr;

                    // Platforms
                    bool win = data["platforms"]?["windows"]?.Value<bool>() ?? true;
                    bool mac = data["platforms"]?["mac"]?.Value<bool>() ?? false;

                    string FormatPrice(string? raw)
                    {
                        if (string.IsNullOrEmpty(raw)) return "";
                        if (raw.StartsWith("$")) return raw.Substring(1) + "$";
                        return raw;
                    }

                    bool isFree = data["is_free"]?.Value<bool>() ?? false;
                    string price = "N/A", origPrice = "";
                    int discount = 0;

                    if (isFree)
                        price = "Free";
                    else if (isReleased || isPreOrder)
                    {
                        var po = data["price_overview"];
                        if (po != null)
                        {

                            price = FormatPrice(po["final_formatted"]?.Value<string>());
                            discount = po["discount_percent"]?.Value<int>() ?? 0;
                            if (discount > 0)
                                origPrice = FormatPrice(po["initial_formatted"]?.Value<string>());
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
                        IsPreOrder = isPreOrder,
                        IsEarlyAccess = data["early_access"]?.Value<bool>() ?? false,
                        DateAddedUnix = dateAdded,
                        PlatformWindows = data["platforms"]?["windows"]?.Value<bool>() ?? true,
                        PlatformMac = data["platforms"]?["mac"]?.Value<bool>() ?? false,
                        Price = price,
                        OriginalPrice = origPrice,
                        DiscountPercent = discount,
                        IsDlc = isDlc,
                        IsFree = isFree
                    });

                    Debug.WriteLine($"[Batch] Fetched: {name} ({appId})");
                    await Task.Delay(400);
                }
                catch (Exception ex) { Debug.WriteLine($"[Batch] Error {appId}: {ex.Message}"); }
            }

            return result;
        }
    }
     
    public class WishlistBatchRequest
    {
        public List<int> AppIds { get; set; } = new();
    }

    public class WishlistItemRef
    {
        public int AppId { get; set; }
        public long DateAdded { get; set; }
    }
}