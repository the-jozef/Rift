using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    /// <summary>
    /// Steam tag dictionary — stiahne raz pri štarte a uloží do local cache.
    /// Steam tag dictionary — downloaded once at startup and saved to local cache.
    /// Použitie: TagService.GetName(tagId) → "Action", "RPG", atď.
    /// Usage: TagService.GetName(tagId) → "Action", "RPG", etc.
    /// </summary>
    public static class TagService
    {
        private static Dictionary<int, string> _tags = new();

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // 18+ tag IDs — presné IDs ktoré Steam používa pre explicitný obsah
        // 18+ tag IDs — exact IDs Steam uses for explicit content
        private static readonly HashSet<int> AdultTagIds = new()
        {
            4085,  // Sexual Content
            6650,  // Nudity
            9077,  // Adult Only
            7208,  // Hentai
            10695, // NSFW
            5350,  // Eroge
            9168,  // Explicit Sexual Content
        };

        // ─── INIT ─────────────────────────────────────────────────────────

        /// <summary>
        /// Inicializuj tag slovník — zavolaj pri štarte apky.
        /// Initialize tag dictionary — call at app startup.
        /// </summary>
        public static async Task InitAsync()
        {
            // Skús z local cache — try from local cache
            var cached = await LocalCacheService.LoadAsync<Dictionary<int, string>>(
                LocalCacheService.KeyTags,
                TimeSpan.FromDays(7)); // Tagy sa menia zriedka — tags change rarely

            if (cached != null && cached.Count > 0)
            {
                _tags = cached;
                Debug.WriteLine($"[Tags] Loaded {_tags.Count} tags from cache.");
                return;
            }

            // Stiahni zo Steam — download from Steam
            await DownloadAsync();
        }

        // ─── PUBLIC API ───────────────────────────────────────────────────

        /// <summary>
        /// Vráti meno tagu podľa ID alebo prázdny string.
        /// Returns tag name by ID or empty string.
        /// </summary>
        public static string GetName(int tagId) =>
            _tags.TryGetValue(tagId, out var name) ? name : string.Empty;

        /// <summary>
        /// Skontroluje či tag je 18+ obsah.
        /// Checks if tag ID is adult content.
        /// </summary>
        public static bool IsAdultTag(int tagId) => AdultTagIds.Contains(tagId);

        /// <summary>
        /// Skontroluje či meno tagu (string) signalizuje 18+ obsah.
        /// Checks if tag name (string) signals adult content.
        /// </summary>
        public static bool IsAdultTagName(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return false;
            var lower = tagName.ToLowerInvariant();
            return lower.Contains("sexual") || lower.Contains("nudity") ||
                   lower.Contains("adult only") || lower.Contains("hentai") ||
                   lower.Contains("nsfw") || lower.Contains("eroge") ||
                   lower.Contains("explicit");
        }

        public static int Count => _tags.Count;

        // ─── PRIVATE ──────────────────────────────────────────────────────

        private static async Task DownloadAsync()
        {
            try
            {
                var url = "https://store.steampowered.com/api/gettaglist/v1/?language=english";
                var response = await _http.GetStringAsync(url);
                var json = JObject.Parse(response);
                var tagsArr = json["response"]?["tags"];

                if (tagsArr == null)
                {
                    Debug.WriteLine("[Tags] No tags in response.");
                    return;
                }

                var dict = new Dictionary<int, string>();
                foreach (var tag in tagsArr)
                {
                    int id = tag["tagid"]?.Value<int>() ?? 0;
                    string name = tag["name"]?.Value<string>() ?? "";
                    if (id > 0 && !string.IsNullOrEmpty(name))
                        dict[id] = name;
                }

                _tags = dict;
                await LocalCacheService.SaveAsync(LocalCacheService.KeyTags, dict);
                Debug.WriteLine($"[Tags] Downloaded and cached {dict.Count} tags.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Tags] Download failed: {ex.Message}");
            }
        }
    }
}