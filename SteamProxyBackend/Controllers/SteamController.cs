using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
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
        private const int RateLimitMs = 500;

        // ─── GAME DETAIL MEMORY CACHE ─────────────────────────────────────
        // Shared across ALL section endpoints — fetch once, serve everywhere
        private static readonly ConcurrentDictionary<int, (StoreGameDto Data, DateTime At)> _detailCache = new();
        private static readonly TimeSpan DetailCacheTTL = TimeSpan.FromHours(6);

        // ─── SECTION LIST CACHE ───────────────────────────────────────────
        // Caches which appIds belong to which section (avoids re-shuffling)
        private static readonly ConcurrentDictionary<string, (List<int> Ids, DateTime At)> _sectionCache = new();
        private static readonly TimeSpan SectionCacheTTL = TimeSpan.FromHours(6);

        // ─── 18+ FILTER ───────────────────────────────────────────────────
        private static readonly HashSet<string> _adultExact = new(StringComparer.OrdinalIgnoreCase)
{
    "hentai","nude","naked","erotic","erotica","eroge","erogi",
    "xxx","porn","pornographic","lewd","nsfw","18+","adult only",
    "fuck","futa","futanari","cumming","horny","🔞","ecchi",
    "ahegao","oppai","yaoi","yuri harem",
    "strip poker","strip club","adult dating","adult visual",
    "dating sim","waifu simulator","girlfriend simulator",
    "visual novel 18","harem sex","perverted",
};
        private static readonly HashSet<string> _adultWords = new(StringComparer.OrdinalIgnoreCase)
{
    "lust","harem","sex","sexy","erotic","naughty",
    "boobs","boob","busty","bikini",
};
        private static readonly System.Text.RegularExpressions.Regex _wordSplit =
    new(@"[^a-zA-Z0-9]+", System.Text.RegularExpressions.RegexOptions.Compiled);

        // ─── EXPANDED CURATED APP IDS ─────────────────────────────────────
        // 200 well-known games — mix of evergreens, recent hits, F2P, discounted
        // Enough for 8 + 24 + 12 + 12 + 25 = 81 unique slots with good variety
        private static readonly int[] CuratedAppIds =
        {
            // ── Evergreens / All-time classics ──────────────────────────────
            730,     // Counter-Strike 2
            570,     // Dota 2
            578080,  // PUBG
            440,     // Team Fortress 2
            550,     // Left 4 Dead 2
            620,     // Portal 2
            400,     // Portal
            220,     // Half-Life 2
            70,      // Half-Life
            4000,    // Garry's Mod
            105600,  // Terraria
            227300,  // Euro Truck Simulator 2
            304930,  // Unturned
            230410,  // Warframe
            391540,  // Undertale
            413150,  // Stardew Valley
            250900,  // The Binding of Isaac: Rebirth
            367520,  // Hollow Knight
            489830,  // Skyrim Special Edition
            377160,  // Fallout 4

            // ── Big recent hits ──────────────────────────────────────────────
            1245620, // Elden Ring
            1091500, // Cyberpunk 2077
            1174180, // Red Dead Redemption 2
            271590,  // GTA V
            1593500, // God of War
            814380,  // Sekiro
            1623730, // Palworld
            2357570, // Lethal Company
            2050650, // Hogwarts Legacy
            1145360, // Hades
            2183900, // Starfield
            1326470, // Sons of the Forest
            526870,  // Satisfactory
            1966720, // The Planet Crafter
            2483980, // Enshrouded
            1063730, // Remnant: From the Ashes
            1326340, // Valheim
            1888160, // The Callisto Protocol
            2308810, // Armored Core VI
            1817070, // Dying Light 2
            2023140, // Returnal
            2246340, // The Last of Us Part I
            1158310, // The Medium
            2540090, // Warhammer 40k: Space Marine 2
            1307580, // Ghostrunner
            1361510, // The Forgotten City
            990080,  // Hogwarts (Legacy alt)
            1281410, // Assassin's Creed Valhalla
            2915570, // Gray Zone Warfare
            2215430, // Dead Island 2

            // ── Multiplayer / Online ─────────────────────────────────────────
            1172470, // Apex Legends
            1938090, // Call of Duty
            252490,  // Rust
            346110,  // ARK: Survival Evolved
            394360,  // Hunt: Showdown
            107410,  // Arma 3
            381210,  // Dead by Daylight
            218620,  // Payday 2
            359550,  // Rainbow Six Siege
            1942660, // Vampire Survivors
            1326410, // Phasmophobia
            1548130, // Back 4 Blood

            // ── F2P ─────────────────────────────────────────────────────────
            1086940, // Baldur's Gate 3
            292030,  // The Witcher 3
            236390,  // War Thunder
            322170,  // Geometry Dash

            // ── Indie / Hidden gems ──────────────────────────────────────────
            646570,  // Slay the Spire
            1250410, // Disco Elysium
            435150,  // Divinity: Original Sin 2
            242760,  // The Forest
            644830,  // For the King
            49520,   // Borderlands 2
            268910,  // Cuphead
            239140,  // Dying Light
            1426210, // It Takes Two
            1446780, // Monster Hunter Rise
            582010,  // Monster Hunter: World
            1675970, // Moondrop
            519860,  // Beat Saber
            261550,  // Subnautica
            949230,  // Subnautica: Below Zero
            284160,  // Besiege
            205100,  // Outlast
            10180,   // Call of Duty: Modern Warfare 2
            221380,  // Age of Empires II HD
            508440,  // Ruiner

            // ── Strategy / Simulation ────────────────────────────────────────
            255710,  // Cities: Skylines
            289070,  // Sid Meier's Civilization VI
            1151340, // Crusader Kings III
            306130,  // The Elder Scrolls Online
            221100,  // DayZ
            311210,  // Call of Duty: Black Ops III
            1238840, // Battlefield 1
            1238810, // Battlefield 4
            1517290, // Battlefield 2042
            1269260, // Battlefield V

            // ── Racing / Sports ──────────────────────────────────────────────
            1551360, // Forza Horizon 5
            1281930, // Forza Horizon 4
            427520,  // Factorio

            // ── RPG ─────────────────────────────────────────────────────────
            1046930, // Assassin's Creed Odyssey
            284160,  // Besiege (alt slot)
            2507950, // Delta Force
            1599340, // Lost Ark
            1794680, // Marvel's Spider-Man Remastered
            1612100, // Ghostwire: Tokyo

            // ── Horror / Thriller ────────────────────────────────────────────
            418370,  // Resident Evil 7
            952060,  // Resident Evil Village
            1196590, // Hades (alt)
            2669320, // Alan Wake 2
            2246460, // Dead Space (2023)
            1794680, // Spider-Man Remastered (alt)

            // ── Action / Adventure ───────────────────────────────────────────
            1091500, // Cyberpunk (alt)
            2064650, // Goose Goose Duck
            1938090, // COD (alt)
            1675600, // Dave the Diver
            2379780, // Lies of P
            1850570, // Atomic Heart
            1517290, // BF 2042 (alt)
            1145360, // Hades (alt)
            2369390, // Final Fantasy XVI
            1817070, // Dying Light 2 (alt)
            2230490, // Vampire: The Masquerade – Bloodhunt
            1812820, // Uncharted Legacy
            1888930, // The Dark Pictures: The Devil in Me
            976730,  // Halo: The Master Chief Collection
            1240440, // Halo Infinite
            2406630, // Star Wars: Jedi Survivor
            1382330, // Persona 4 Golden
            1461830, // Persona 3 Portable
            1687950, // Persona 5 Royal
            2461750, // Hi-Fi Rush
            1449560, // Weird West
            1637320, // Against the Storm
            1604030, // V Rising
            1551360, // Forza 5 (alt)
            1868140, // Temtem
            2504880, // The Last of Us Part II
            1782210, // High on Life
            1259380, // Sifu
            2283350, // Dredge
            1623340, // Trepang2
            2379850, // Blasphemous 2
            2348590, // Warhammer 40k: Darktide
            1449540, // Darkest Dungeon 2
            1794960, // Choo-Choo Charles
            2528740, // Anger Foot
            2567870, // Black Myth: Wukong
            2358720, // Robocop: Rogue City
            2677660, // Suicide Squad: Kill the Justice League
            2358720, // Robocop (alt)
            2881650, // Indiana Jones and the Great Circle
        };

        // ─── TAG → appIds mapping (for "by tag" section) ──────────────────
        // Pre-computed so we don't have to fetch all games just to filter
        private static readonly Dictionary<string, int[]> TagAppIds = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = new[] { 730, 1245620, 1091500, 2308810, 814380, 2357570, 1307580, 1942660, 1326410, 239140, 268910, 1817070, 2246340, 2379780, 1850570, 2567870 },
            ["RPG"] = new[] { 1086940, 292030, 1245620, 435150, 1250410, 489830, 1593500, 2183900, 1687950, 1382330, 1461830, 1151340, 2369390, 1687950, 2379780 },
            ["Survival"] = new[] { 252490, 346110, 1326340, 1966720, 2483980, 242760, 1326470, 221100, 105600, 261550, 949230, 1604030 },
            ["Multiplayer"] = new[] { 730, 570, 578080, 440, 550, 1172470, 1938090, 394360, 381210, 218620, 359550, 1548130, 236390, 1604030 },
            ["Strategy"] = new[] { 255710, 289070, 1151340, 221380, 427520, 644830, 227300, 413150, 1637320, 646570 },
            ["Horror"] = new[] { 381210, 205100, 1326410, 418370, 952060, 2246460, 2669320, 1794960, 1888930 },
            ["Indie"] = new[] { 391540, 367520, 646570, 1250410, 105600, 1145360, 413150, 268910, 1675600, 2283350, 2379850, 1637320 },
            ["Simulation"] = new[] { 227300, 255710, 427520, 413150, 526870, 4000, 1966720, 2483980 },
        };

        public SteamController(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _http = httpFactory.CreateClient();
            _steamApiKey = Environment.GetEnvironmentVariable("SteamApiKey")
                ?? config["SteamApiKey"]
                ?? throw new Exception("SteamApiKey not configured.");
        }

        // ─── HELPERS ──────────────────────────────────────────────────────

        private static string FormatMinutes(int minutes)
        {
            if (minutes <= 0) return "0 hrs";
            if (minutes < 60) return $"{minutes} mins";
            double h = minutes / 60.0;
            return h % 1 == 0 ? $"{(int)h} hrs" : $"{h:F1} hrs";
        }
        private static string FormatPrice(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "N/A") return raw;
            raw = raw.Trim();
            string symbol = raw.Contains("$") ? "$" : raw.Contains("€") ? "€" : "";
            string number = raw.Replace("$", "").Replace("€", "").Trim();
            return string.IsNullOrEmpty(symbol) ? number : $"{number}{symbol}";
        }
        private bool IsRateLimited(string ip)
        {
            // Key = IP + endpoint path — parallel calls to different endpoints are not blocked
            var key = $"{ip}:{Request.Path}";
            if (_lastRequestTime.TryGetValue(key, out var last))
                if ((DateTime.UtcNow - last).TotalMilliseconds < RateLimitMs) return true;
            _lastRequestTime[key] = DateTime.UtcNow;
            return false;
        }

        private string GetClientIp() =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private static bool HasAdultName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            // Fast exact substring check (handles multi-word phrases too)
            var lower = name.ToLowerInvariant();
            foreach (var kw in _adultExact)
                if (lower.Contains(kw)) return true;

            // Whole-word check so "sex" doesn't block "Sexsmith County" but does block "Sex Game"
            var words = _wordSplit.Split(lower);
            foreach (var word in words)
                if (_adultWords.Contains(word)) return true;

            return false;
        }

        private static bool HasExplicitContent(JToken? data)
        {
            if (data == null) return false;
            var ids = data["content_descriptors"]?["ids"]?.ToObject<List<int>>() ?? new();
            return ids.Contains(1) || ids.Contains(3) || ids.Contains(4);
        }

        // ─── DTO ──────────────────────────────────────────────────────────

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
            public List<string> Developers { get; set; } = new();
            public List<string> Publishers { get; set; } = new();
            public string ReleaseDate { get; set; } = "";
            public string ReviewDesc { get; set; } = "";
            public string ReviewCss { get; set; } = "";
        }

        // ─── CORE FETCH — single game, memory cached ──────────────────────────────────────────────────────
        private async Task<StoreGameDto?> FetchSingleAsync(int appId)
        {
            // Memory cache hit — instant return
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

                string appType = data["type"]?.Value<string>()?.ToLower() ?? "game";
                if (appType == "dlc")
                {
                    Debug.WriteLine($"[Store] Skipping DLC: {name} ({appId})");
                    return null;
                }
                if (string.IsNullOrEmpty(name)) return null;
                if (HasAdultName(name) || HasExplicitContent(data)) return null;

                // ─── Pricing ──────────────────────────────────────────────
                bool isFree = data["is_free"]?.Value<bool>() ?? false;
                string price = "N/A", origPrice = "";
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
                        price = FormatPrice(po["final_formatted"]?.Value<string>() ?? "N/A");
                        discount = po["discount_percent"]?.Value<int>() ?? 0;
                        if (discount > 0)
                            origPrice = FormatPrice(po["initial_formatted"]?.Value<string>() ?? "");
                    }
                }

                // ─── Genres & Tags ────────────────────────────────────────
                var genres = (data["genres"] as JArray)?
                    .Select(g => g["description"]?.Value<string>() ?? "")
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Take(3).ToList() ?? new();

                var keyCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Single-player", "Multi-player", "Co-op", "Online Co-op",
                    "PvP", "Online PvP", "Early Access", "Co-op Campaign",
                    "Cross-Platform Multiplayer"
                };
                var catTags = (data["categories"] as JArray)?
                    .Select(c => c["description"]?.Value<string>() ?? "")
                    .Where(t => keyCats.Contains(t))
                    .Take(2).ToList() ?? new();

                var tags = genres.Concat(catTags).Distinct().Take(4).ToList();

                // ─── Screenshots (first 4 only) ───────────────────────────
                var screenshots = (data["screenshots"] as JArray)?
                    .Select(s => s["path_full"]?.Value<string>() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Take(4).ToList() ?? new();

                // ─── Reviews ──────────────────────────────────────────────
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
                    (reviewDesc, reviewCss) = await FetchReviewsAsync(appId);
                if (string.IsNullOrEmpty(reviewDesc))
                    reviewDesc = "No Reviews";

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
                    Description = data["short_description"]?.Value<string>() ?? "",
                    SteamStoreUrl = $"https://store.steampowered.com/app/{appId}",
                    Developers = (data["developers"] as JArray)?
                                         .Select(d => d.Value<string>() ?? "")
                                         .Where(d => !string.IsNullOrEmpty(d)).ToList() ?? new(),
                    Publishers = (data["publishers"] as JArray)?
                                         .Select(p => p.Value<string>() ?? "")
                                         .Where(p => !string.IsNullOrEmpty(p)).ToList() ?? new(),
                    ReleaseDate = data["release_date"]?["date"]?.Value<string>() ?? "",
                    ReviewDesc = reviewDesc,
                    ReviewCss = reviewCss,
                };

                _detailCache[appId] = (dto, DateTime.UtcNow);
                return dto;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Store] Fetch {appId}: {ex.Message}");
                return null;
            }
        }

        // ─── SECTION BUILDER — picks N unique games from a pool ──────────────────────────────────────────────────────
        //  Uses a per-section seed so each section shows different games
        //  Uses SectionCache so the same request doesn't re-shuffle 
        private async Task<List<StoreGameDto>> BuildSectionAsync(string sectionKey, int[] pool, int count, int skip = 0, bool discountedOnly = false)
        {
            var orderedIds = GetOrBuildSectionOrder(sectionKey, pool);
            var result = new List<StoreGameDto>();
            int skipped = 0;
            int batchSize = 10; // fetch 10 at a time in parallel

            using var semaphore = new SemaphoreSlim(4, 4); // max 4 concurrent Steam API calls

            for (int i = 0; i < orderedIds.Count && result.Count < count; i += batchSize)
            {
                var batch = orderedIds.Skip(i).Take(batchSize).ToList();

                // Separate already-cached from needs-fetch
                var cached = new Dictionary<int, StoreGameDto>();
                var toFetch = new List<int>();

                foreach (var id in batch)
                {
                    if (_detailCache.TryGetValue(id, out var ce) &&
                        DateTime.UtcNow - ce.At < DetailCacheTTL)
                        cached[id] = ce.Data;
                    else
                        toFetch.Add(id);
                }

                // Fetch non-cached in parallel
                var fetchedMap = new System.Collections.Concurrent.ConcurrentDictionary<int, StoreGameDto>();
                if (toFetch.Count > 0)
                {
                    var tasks = toFetch.Select(async id =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            await Task.Delay(60); // small polite delay per concurrent request
                            var dto = await FetchSingleAsync(id);
                            if (dto != null) fetchedMap[id] = dto;
                        }
                        finally { semaphore.Release(); }
                    });
                    await Task.WhenAll(tasks);
                }

                // Merge results in original order
                foreach (var id in batch)
                {
                    if (result.Count >= count) break;
                    var dto = cached.TryGetValue(id, out var c) ? c
                            : fetchedMap.TryGetValue(id, out var f) ? f
                            : null;
                    if (dto == null) continue;
                    if (discountedOnly && !dto.HasDiscount && !dto.IsFree) continue;
                    if (skipped < skip) { skipped++; continue; }
                    result.Add(dto);
                }
            }

            return result;
        }

        // Returns a stable daily shuffle of the pool for a given section key.
        // Same section always gets the same order within a day → consistent pagination.
        private List<int> GetOrBuildSectionOrder(string key, int[] pool)
        {
            if (_sectionCache.TryGetValue(key, out var sc) &&
                DateTime.UtcNow - sc.At < SectionCacheTTL)
                return sc.Ids;

            int seed = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"))
                       + key.GetHashCode();
            var rng = new Random(seed);
            var ids = pool.Distinct().OrderBy(_ => rng.Next()).ToList();

            _sectionCache[key] = (ids, DateTime.UtcNow);
            return ids;
        }

        // ─── ENDPOINTS ──────────────────────────────────────────────────────

        // ── Featured — 8 games, diverse, no filter ────────────────────────
        [HttpGet("store/featured")]
        public async Task<IActionResult> GetFeatured()
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var games = await BuildSectionAsync("featured", CuratedAppIds, count: 8);
                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ── Discounts — 24 discounted games ───────────────────────────────
        // Returns 24 at once; client paginates with arrows (8 visible at a time)
        [HttpGet("store/discounts")]
        public async Task<IActionResult> GetDiscounts()
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                // Try discounted-only first
                var games = await BuildSectionAsync(
                    "discounts", CuratedAppIds, count: 24, discountedOnly: true);

                // Pad with non-discounted if not enough
                if (games.Count < 24)
                {
                    var existing = new HashSet<int>(games.Select(g => g.AppId));
                    var pad = await BuildSectionAsync(
                        "discounts_pad", CuratedAppIds, count: 24 - games.Count);
                    games.AddRange(pad.Where(g => !existing.Contains(g.AppId)));
                }

                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ── Recommended — 12 games based on player's library genres ───────
        [HttpGet("store/recommended")]
        public async Task<IActionResult> GetRecommended([FromQuery] string genres = "")
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                // Parse genre list from client
                var genreList = genres
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(g => g.Trim())
                    .Where(g => !string.IsNullOrEmpty(g))
                    .ToList();

                // Build a targeted pool from matching tags
                var pool = new List<int>();
                foreach (var genre in genreList)
                {
                    if (TagAppIds.TryGetValue(genre, out var tagIds))
                        pool.AddRange(tagIds);
                }

                // Fallback to full list if no matches
                if (pool.Count < 12)
                    pool.AddRange(CuratedAppIds);

                var games = await BuildSectionAsync(
                    "recommended", pool.Distinct().ToArray(), count: 12);

                // Mark as recommended
                foreach (var g in games)
                {
                    g.IsRecommended = true;
                    g.StatusText = "Recommended";
                }

                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ── By Tag — 12 games matching a specific tag ─────────────────────
        [HttpGet("store/bytag")]
        public async Task<IActionResult> GetByTag([FromQuery] string tag = "Action")
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var pool = TagAppIds.TryGetValue(tag, out var tagIds)
                    ? tagIds.Concat(CuratedAppIds).Distinct().ToArray()
                    : CuratedAppIds;

                // Return 16 — client filters used IDs, leaving ≥12 after overlap removal
                var games = await BuildSectionAsync($"bytag_{tag}", pool, count: 16);
                return Ok(new { Games = games, Tag = tag });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ── More — paginated section, 5 per page, max 5 pages (25 total) ──
        // page=0 → items 1-5, page=1 → items 6-10 ... page=4 → items 21-25
        [HttpGet("store/more")]
        public async Task<IActionResult> GetMore([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                const int pageSize = 8;   // backend returns 8, client shows ~5 after filtering
                const int maxPages = 4;   // 4 × 8 = 32 total candidates

                if (page < 0 || page >= maxPages)
                    return Ok(new { Games = new List<object>(), HasMore = false, Page = page });

                // "more" key → same stable daily shuffle for every page
                // skip = page × pageSize so pages are non-overlapping slices
                var games = await BuildSectionAsync(
                    "more", CuratedAppIds, count: pageSize, skip: page * pageSize);

                bool hasMore = (page + 1) < maxPages;
                return Ok(new { Games = games, HasMore = hasMore, Page = page });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ── New Trending (kept for backward compat) ───────────────────────
        [HttpGet("store/newtrending")]
        public async Task<IActionResult> GetNewTrending([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var games = await BuildSectionAsync(
                    "newtrending", CuratedAppIds, count: 8, skip: page * 8);
                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ── Top Sellers (kept for backward compat) ────────────────────────
        [HttpGet("store/topsellers")]
        public async Task<IActionResult> GetTopSellers([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var games = await BuildSectionAsync(
                    "topsellers", CuratedAppIds, count: 8, skip: page * 8);
                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ── Specials (kept for backward compat) ───────────────────────────
        [HttpGet("store/specials")]
        public async Task<IActionResult> GetSpecials([FromQuery] int page = 0)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var games = await BuildSectionAsync(
                    "specials", CuratedAppIds, count: 8,
                    skip: page * 8, discountedOnly: true);
                return Ok(new { Games = games });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ── Single game detail ─────────────────────────────────────────────
        [HttpGet("game/{appId}")]
        public async Task<IActionResult> GetGameDetails(int appId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var dto = await FetchSingleAsync(appId);
                if (dto == null) return NotFound(new { Message = "Game not found or filtered." });
                return Ok(dto);
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // ── Library game info ──────────────────────────────────────────────
        [HttpGet("library/game/{appId}/info")]
        public async Task<IActionResult> GetLibraryGameInfo(int appId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var dto = await FetchSingleAsync(appId);
                if (dto == null) return NotFound(new { Message = "Game not found." });
                return Ok(dto);
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        [HttpGet("player/{steamId}")]
        public async Task<IActionResult> GetPlayerSummary(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

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
        // GET /api/steam/player/{steamId}/level
        [HttpGet("player/{steamId}/level")]
        public async Task<IActionResult> GetSteamLevel(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var url = $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/" +
                          $"?key={_steamApiKey}&steamid={steamId}";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                int level = json["response"]?["player_level"]?.Value<int>() ?? 0;
                return Ok(new { Level = level });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // GET /api/steam/player/{steamId}/friends
        [HttpGet("player/{steamId}/friends")]
        public async Task<IActionResult> GetFriendsList(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                // 1. Získaj zoznam friend steamId-čiek
                var friendsUrl = $"https://api.steampowered.com/ISteamUser/GetFriendList/v1/" +
                                 $"?key={_steamApiKey}&steamid={steamId}&relationship=friend";
                string friendsJson;
                try
                {
                    friendsJson = await _http.GetStringAsync(friendsUrl);
                }
                catch
                {
                    // Private profile — vrátime špeciálny flag
                    return Ok(new { IsPrivate = true, Friends = new List<object>() });
                }

                var friendsData = JObject.Parse(friendsJson);
                var friendsList = friendsData["friendslist"]?["friends"] as JArray;

                if (friendsList == null || !friendsList.Any())
                    return Ok(new { IsPrivate = false, Friends = new List<object>() });

                // 2. Zoberieme max 100 (Steam API limit pre GetPlayerSummaries)
                var friendIds = friendsList
                    .Select(f => f["steamid"]?.Value<string>() ?? "")
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Take(100)
                    .ToList();

                // 3. Batch fetch summaries
                var ids = string.Join(",", friendIds);
                var summaryUrl = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/" +
                                 $"?key={_steamApiKey}&steamids={ids}";
                var summaryJson = await _http.GetStringAsync(summaryUrl);
                var summaryData = JObject.Parse(summaryJson);
                var players = summaryData["response"]?["players"] as JArray;

                if (players == null)
                    return Ok(new { IsPrivate = false, Friends = new List<object>() });

                // 4. Mapuj persona state
                static string MapState(int state, string? gameId) =>
                    !string.IsNullOrEmpty(gameId) && gameId != "0" ? "In-Game" :
                    state switch
                    {
                        1 => "Online",
                        2 => "Busy",
                        3 => "Away",
                        4 => "Snooze",
                        5 => "Looking to Trade",
                        6 => "Looking to Play",
                        _ => "Offline"
                    };

                var result = players.Select(p => new
                {
                    SteamId = p["steamid"]?.Value<string>() ?? "",
                    Username = p["personaname"]?.Value<string>() ?? "",
                    AvatarUrl = p["avatarfull"]?.Value<string>() ?? "",
                    Status = MapState(
                                    p["personastate"]?.Value<int>() ?? 0,
                                    p["gameid"]?.Value<string>()),
                    CurrentGame = p["gameextrainfo"]?.Value<string>() ?? "",
                    IsOnline = (p["personastate"]?.Value<int>() ?? 0) != 0
                })
                // Online/In-Game first, then alphabetically
                .OrderByDescending(f => f.Status == "In-Game")
                .ThenByDescending(f => f.IsOnline)
                .ThenBy(f => f.Username)
                .ToList();

                return Ok(new { IsPrivate = false, Friends = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // GET /api/steam/player/{steamId}/recentactivity
        [HttpGet("player/{steamId}/recentactivity")]
        public async Task<IActionResult> GetRecentActivity(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var url = $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/" +
                          $"?key={_steamApiKey}&steamid={steamId}&count=5";

                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var games = json["response"]?["games"] as JArray;

                if (games == null || !games.Any())
                    return Ok(new { TotalHours = 0.0, Games = new List<object>() });

                double totalMinutes2Weeks = 0;
                var result = new List<object>();

                foreach (var g in games)
                {
                    int appId = g["appid"]?.Value<int>() ?? 0;
                    string name = g["name"]?.Value<string>() ?? "";
                    int playtime2w = g["playtime_2weeks"]?.Value<int>() ?? 0;
                    int playtimeTotal = g["playtime_forever"]?.Value<int>() ?? 0;

                    totalMinutes2Weeks += playtime2w;

                    // Header image
                    string headerUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg";

                    result.Add(new
                    {
                        AppId = appId,
                        Name = name,
                        HeaderImageUrl = headerUrl,
                        Playtime2Weeks = playtime2w,
                        PlaytimeTotal = playtimeTotal,
                        // Display helpers
                        Playtime2WeeksDisplay = FormatMinutes(playtime2w),
                        PlaytimeTotalDisplay = FormatMinutes(playtimeTotal),
                    });
                }

                double totalHours2Weeks = Math.Round(totalMinutes2Weeks / 60.0, 1);

                return Ok(new
                {
                    TotalHours = totalHours2Weeks,
                    Games = result
                });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        // GET /api/steam/store/search?q=xxx
        [HttpGet("store/search")]
        public async Task<IActionResult> SearchGames([FromQuery] string q = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                    return Ok(new { Results = new List<object>() });

                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                // Primary: Steam Store search API — name + price in ONE call, no second request needed.
                // Fallback: Steam Community search API (older, returns only names).
                List<object> results = await SearchViaStoreApiAsync(q);

                if (results.Count == 0)
                    results = await SearchViaCommunityAsync(q);

                return Ok(new { Results = results });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Search] Error: {ex.Message}");
                return StatusCode(500, new { Message = ex.Message });
            }
        }

        private async Task<List<object>> SearchViaStoreApiAsync(string q)
        {
            try
            {
                var url = $"https://store.steampowered.com/api/storesearch/" +
                          $"?term={Uri.EscapeDataString(q)}&l=english&cc=sk&ignore_preferences=1";
                var json = await _http.GetStringAsync(url);
                var data = JObject.Parse(json);
                var items = data["items"] as JArray;
                if (items == null || !items.Any()) return new();

                var results = new List<object>();

                foreach (var item in items.Take(8))
                {
                    bool isComingSoon = false;

                    int appId = item["id"]?.Value<int>() ?? 0;
                    if (appId <= 0) continue;

                    string name = item["name"]?.Value<string>() ?? "";
                    if (string.IsNullOrEmpty(name) || HasAdultName(name)) continue;

                    // Always build from CDN — storesearch tiny_image is too small
                    string headerUrl =
                        $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg";

                    var priceObj = item["price"];
                    bool isFree = false;
                    string price = "";
                    string origPrice = "";
                    int discount = 0;

                    if (priceObj == null)
                    {
                        price = "";
                        isComingSoon = true;   // pridaj túto premennú do metódy
                    }
                    else
                    {
                        int finalCents = priceObj["final"]?.Value<int>() ?? -1;
                        discount = priceObj["discount_percent"]?.Value<int>() ?? 0;

                        if (finalCents <= 0)
                        {
                            // final = 0  → definitively free
                            isFree = true;
                            price = "Free To Play";
                        }
                        else
                        {
                            // Paid game — may or may not have a discount
                            price = priceObj["final_formatted"]?.Value<string>() ?? "";
                            if (discount > 0)
                                origPrice = priceObj["initial_formatted"]?.Value<string>() ?? "";
                        }
                    }

                    results.Add(new
                    {
                        AppId = appId,
                        Name = name,
                        HeaderImageUrl = headerUrl,
                        Price = price,
                        OriginalPrice = origPrice,
                        DiscountPercent = discount,
                        IsFree = isFree,
                        IsComingSoon = isComingSoon,
                        HasDiscount = discount > 0 && !isFree
                    });
                }
                return results;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Search] storesearch failed: {ex.Message}");
                return new();
            }
        }

        private async Task<List<object>> SearchViaCommunityAsync(string q)
        {
            try
            {
                var url = $"https://steamcommunity.com/actions/SearchApps/{Uri.EscapeDataString(q)}";
                var json = await _http.GetStringAsync(url);
                var arr = JArray.Parse(json);

                var results = new List<object>();
                foreach (var item in arr.Take(6))
                {
                    int appId = item["appid"]?.Value<int>() ?? 0;
                    if (appId <= 0) continue;

                    string name = item["name"]?.Value<string>() ?? "";
                    if (string.IsNullOrEmpty(name) || HasAdultName(name)) continue;

                    // Try memory cache first for price, skip extra API call if not cached
                    string price = "N/A"; bool isFree = false; int discount = 0; string orig = "";
                    if (_detailCache.TryGetValue(appId, out var ce) && DateTime.UtcNow - ce.At < DetailCacheTTL)
                    {
                        price = ce.Data.Price;
                        isFree = ce.Data.IsFree;
                        discount = ce.Data.DiscountPercent;
                        orig = ce.Data.OriginalPrice;
                    }

                    results.Add(new
                    {
                        AppId = appId,
                        Name = name,
                        HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                        Price = price,
                        OriginalPrice = orig,
                        DiscountPercent = discount,
                        IsFree = isFree,
                        HasDiscount = discount > 0
                    });
                }
                return results;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Search] community fallback failed: {ex.Message}");
                return new();
            }
        }

        [HttpGet("library/{steamId}")]
        public async Task<IActionResult> GetOwnedGames(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/" +
                          $"?key={_steamApiKey}&steamid={steamId}" +
                          $"&include_appinfo=true&include_played_free_games=true&format=json";

                var response = await _http.GetStringAsync(url);
                var data = JsonConvert.DeserializeObject<dynamic>(response);
                var games = data?.response?.games;

                if (games == null) return Ok(new { Games = new List<object>() });

                var result = new List<object>();
                foreach (var game in games)
                {
                    string name = (string)(game.name ?? "");
                    if (string.IsNullOrEmpty(name)) continue;

                    int appId = (int)game.appid;
                    int playtime = (int)(game.playtime_forever ?? 0);

                    string type = "game";
                    string nameLow = name.ToLower();
                    if (nameLow.Contains("demo")) type = "demo";
                    else if (nameLow.Contains("prologue")) type = "prologue";
                    else if (nameLow.Contains("playtest")) type = "playtest";
                    else if (nameLow.Contains(" beta")) type = "beta";
                    else if (nameLow.Contains("soundtrack")) type = "soundtrack";
                    else if (nameLow.Contains("dedicated server") ||
                             nameLow.Contains(" sdk") ||
                             nameLow.Contains(" tool")) type = "tool";

                    result.Add(new
                    {
                        AppId = appId,
                        Name = name,
                        Type = type,
                        PlaytimeMinutes = playtime,
                        IconUrl = $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{game.img_icon_url}.jpg",
                        HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
                    });
                }

                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

        [HttpGet("library/{steamId}/full")]
        public async Task<IActionResult> GetFullLibrary(string steamId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://steamcommunity.com/profiles/{steamId}/games/?tab=all&xml=1");
                request.Headers.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                request.Headers.Add("Accept", "text/xml,application/xml,*/*");

                var response = await _http.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!content.TrimStart().StartsWith("<?xml") &&
                    !content.TrimStart().StartsWith("<gamesList"))
                    return Ok(new { Games = new List<object>(), Error = "Not XML" });

                if (content.Contains("profile is private") ||
                    content.Contains("privacyMessage"))
                    return Ok(new { Games = new List<object>(), Error = "Profile is private" });

                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(content);
                var games = doc.SelectNodes("//game");
                if (games == null || games.Count == 0)
                    return Ok(new { Games = new List<object>() });

                var result = new List<object>();
                foreach (System.Xml.XmlNode game in games)
                {
                    var appIdStr = game["appID"]?.InnerText;
                    var name = game["name"]?.InnerText;
                    if (!int.TryParse(appIdStr, out int appId) || appId <= 0) continue;
                    if (string.IsNullOrEmpty(name)) continue;

                    var hoursRaw = game["hoursOnRecord"]?.InnerText ?? "0";
                    double.TryParse(hoursRaw.Replace(",", ""),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double hours);

                    result.Add(new
                    {
                        AppId = appId,
                        Name = name,
                        PlaytimeMinutes = (int)(hours * 60),
                        IconUrl = $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/capsule_sm_120.jpg",
                        HeaderImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
                    });
                }

                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

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
                        var n = a["name"]?.Value<string>() ?? "";
                        iconLookup[n] = (a["icon"]?.Value<string>() ?? "", a["icongray"]?.Value<string>() ?? "");
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
                            var n = a["name"]?.Value<string>() ?? "";
                            var pct = a["percent"]?.Value<double>() ?? 100.0;
                            if (!string.IsNullOrEmpty(n)) percentLookup[n] = pct;
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
                var achs = data["game"]?["availableGameStats"]?["achievements"];

                if (achs == null) return Ok(new { Achievements = new List<object>() });

                var result = achs.Select(a => new
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

        [HttpGet("playerstats/{appId}/{steamId}")]
        public async Task<IActionResult> GetPlayerStats(int appId, string steamId)
        {
            try
            {
                var url = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?key={_steamApiKey}&appid={appId}&steamid={steamId}&l=en";
                var response = await _http.GetStringAsync(url);
                var data = JObject.Parse(response);
                var achs = data["playerstats"]?["achievements"];
                if (achs == null) return Ok(new { Achievements = new List<object>() });

                var result = achs.Select(a => new
                {
                    ApiName = a["apiname"]?.Value<string>() ?? "",
                    Achieved = a["achieved"]?.Value<int>() == 1,
                    UnlockTime = a["unlocktime"]?.Value<long>() ?? 0
                }).ToList();

                return Ok(new { Achievements = result });
            }
            catch { return Ok(new { Achievements = new List<object>() }); }
        }

        [HttpGet("wishlist/{steamId}/ids")]
        public async Task<IActionResult> GetWishlistIds(string steamId)
        {
            try
            {
                if (IsRateLimited(GetClientIp()))
                    return StatusCode(429, new { Message = "Too many requests." });

                var url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1/?key={_steamApiKey}&steamid={steamId}";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var items = json["response"]?["items"] as JArray;

                if (items == null || !items.Any())
                    return Ok(new { Items = new List<object>() });

                var result = items
                    .Select(i => new { AppId = i["appid"]?.Value<int>() ?? 0, DateAdded = i["date_added"]?.Value<long>() ?? 0 })
                    .Where(x => x.AppId > 0).ToList();

                return Ok(new { Items = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
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
                return Ok(new { Games = result });
            }
            catch (Exception ex) { return StatusCode(500, new { Message = ex.Message }); }
        }

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

                var req = new HttpRequestMessage(HttpMethod.Post,
                    "https://store.steampowered.com/api/removefromwishlist")
                { Content = content };
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

        // ─── PRIVATE HELPERS ──────────────────────────────────────────────
        private async Task<(string desc, string css)> FetchReviewsAsync(int appId)
        {
            try
            {
                var url = $"https://store.steampowered.com/appreviews/{appId}?json=1&language=all&review_type=all&purchase_type=all&num_per_page=0";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var summary = json["query_summary"];
                if (summary == null) return ("No Reviews", "");

                int total = summary["total_reviews"]?.Value<int>() ?? 0;
                if (total == 0) return ("No Reviews", "");

                var desc = summary["review_score_desc"]?.Value<string>() ?? "";
                if (!string.IsNullOrEmpty(desc) && desc != "No user reviews")
                    return desc switch
                    {
                        "Very Positive" => ("Very Positive", "veryPositive"),
                        "Positive" => ("Positive", "positive"),
                        "Mostly Positive" => ("Mostly Positive", "mostlyPositive"),
                        "Mixed" => ("Mixed", "mixed"),
                        "Mostly Negative" => ("Mostly Negative", "mostlyNegative"),
                        "Very Negative" => ("Very Negative", "veryNegative"),
                        _ => ("No Reviews", "")
                    };

                int pos = summary["total_positive"]?.Value<int>() ?? 0;
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
            catch { return ("No Reviews", ""); }
        }

        private async Task<List<object>> FetchBatchAppDetailsAsync(List<int> appIds, Dictionary<int, long> dateAddedLookup)
        {
            var result = new System.Collections.Concurrent.ConcurrentBag<(int order, object item)>();
            var usedTags = new System.Collections.Concurrent.ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using var semaphore = new SemaphoreSlim(3, 3); // 3 concurrent — respect Steam rate limit

            var tasks = appIds.Select(async (appId, idx) =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await Task.Delay(150); // polite delay
                    var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=sk&l=en";
                    var response = await _http.GetStringAsync(url);
                    var json = JObject.Parse(response);
                    var entry = json[appId.ToString()];

                    if (entry?["success"]?.Value<bool>() != true) return;
                    var data = entry["data"];
                    if (data == null) return;

                    string name = data["name"]?.Value<string>() ?? "";
                    if (string.IsNullOrEmpty(name) || HasAdultName(name) || HasExplicitContent(data)) return;

                    string appType = data["type"]?.Value<string>()?.ToLower() ?? "game";
                    bool isDlc = appType == "dlc";
                    // ── Tags ──
                    var popularCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Single-player", "Multi-player", "Co-op", "Online Co-op", "PvP", "Online PvP", "Co-op Campaign"
            };
                    var genreTags = (data["genres"] as JArray)?
                        .Select(g => g["description"]?.Value<string>() ?? "")
                        .Where(t => !string.IsNullOrEmpty(t)).ToList() ?? new();
                    var catTags = (data["categories"] as JArray)?
                        .Select(c => c["description"]?.Value<string>() ?? "")
                        .Where(t => !string.IsNullOrEmpty(t) && popularCats.Contains(t)).ToList() ?? new();

                    var tags = genreTags.Concat(catTags)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(t => usedTags.TryGetValue(t, out var c) ? c : 0)
                        .Take(4).ToList();

                    foreach (var tag in tags)
                        usedTags.AddOrUpdate(tag, 1, (_, v) => v + 1);

                    // ── Reviews ──
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
                        (reviewDesc, reviewCss) = await FetchReviewsAsync(appId);
                    if (string.IsNullOrEmpty(reviewDesc))
                        reviewDesc = "No Reviews";

                    // ── Release / Pre-order ──
                    bool comingSoon = data["release_date"]?["coming_soon"]?.Value<bool>() ?? false;
                    bool isReleased = !comingSoon;
                    string releaseStr = data["release_date"]?["date"]?.Value<string>() ?? "";
                    long relDateUnix = 0;
                    string relDateDisplay;

                    if (!isReleased)
                        relDateDisplay = string.IsNullOrEmpty(releaseStr) ? "Coming Soon" : releaseStr;
                    else if (DateTime.TryParse(releaseStr, out var dt))
                    {
                        relDateUnix = new DateTimeOffset(dt).ToUnixTimeSeconds();
                        relDateDisplay = dt.ToString("M/d/yyyy");
                    }
                    else
                        relDateDisplay = releaseStr;

                    // ── Price ──
                    bool isFree = data["is_free"]?.Value<bool>() ?? false;
                    string price = "N/A", origPrice = "";
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
                            price = FormatPrice(po["final_formatted"]?.Value<string>() ?? "N/A");
                            discount = po["discount_percent"]?.Value<int>() ?? 0;
                            if (discount > 0)
                                origPrice = FormatPrice(po["initial_formatted"]?.Value<string>() ?? "");
                        }
                    }

                    // ── IsPreOrder — game has a price but isn't released yet ──
                    bool isPreOrder = comingSoon
                                   && !isFree
                                   && price != "N/A"
                                   && !string.IsNullOrEmpty(price);

                    long dateAdded = dateAddedLookup.TryGetValue(appId, out var da) ? da : 0;

                    result.Add((idx, (object)new
                    {
                        AppId = appId,
                        Name = name,
                        HeaderImageUrl = data["header_image"]?.Value<string>()?? $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
                        Tags = tags,
                        ReviewDesc = reviewDesc,
                        ReviewCss = reviewCss,
                        ReleaseDateUnix = relDateUnix,
                        ReleaseDateDisplay = relDateDisplay,
                        IsReleased = isReleased,
                        IsPreOrder = isPreOrder,
                        DateAddedUnix = dateAdded,
                        PlatformWindows = data["platforms"]?["windows"]?.Value<bool>() ?? true,
                        PlatformMac = data["platforms"]?["mac"]?.Value<bool>() ?? false,
                        Price = price,
                        OriginalPrice = origPrice,
                        DiscountPercent = discount,
                        IsFree = isFree,
                        IsDlc = isDlc      
                    }));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Batch] Error {appId}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Return in original order
            return result.OrderBy(r => r.order).Select(r => r.item).ToList();
        }

        // ─── REQUEST TYPES ────────────────────────────────────────────────────
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
}