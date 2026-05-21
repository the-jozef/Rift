using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Rift_App.Services;
using Steamworks;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    public static class LastPlayedCacheService
    {
        private static readonly TimeSpan TTL = TimeSpan.FromHours(2);

        private static Dictionary<int, DateTime?> _mem = new();
        private static DateTime _loadedAt = DateTime.MinValue;
        private static bool _initialized = false;

        // ─── Cache path — resolved lazily so Steamworks is ready ─────────
        private static string CachePath
        {
            get
            {
                // Prefer SessionManager (always set during normal use).
                // Fall back to Steamworks for the rare early-init case.
                string steamId = SessionManager.SteamId64;
                if (string.IsNullOrEmpty(steamId))
                {
                    try { steamId = SteamUser.GetSteamID().m_SteamID.ToString(); }
                    catch { steamId = "unknown"; }
                }
                return Path.Combine(AppPaths.UserCache(steamId), "lastplayed.json");
            }
        }

        private class CacheWrapper
        {
            public DateTime SavedAt { get; set; }
            public Dictionary<int, long> Data { get; set; } = new();
        }

        // ─── PUBLIC ───────────────────────────────────────────────────────

        public static async Task InitializeAsync()
        {
            if (_initialized && DateTime.UtcNow - _loadedAt < TTL) return;

            if (await TryLoadDiskAsync()) return;
            await LoadVdfAsync();
        }

        public static DateTime? Get(int appId)
        {
            _mem.TryGetValue(appId, out var v);
            return v;
        }

        public static async Task RefreshAsync()
        {
            await LoadVdfAsync();
            Debug.WriteLine("[LastPlayedCache] Refreshed from VDF.");
        }

        public static void Set(int appId, DateTime? value) => _mem[appId] = value;

        // ─── PRIVATE ──────────────────────────────────────────────────────

        private static async Task<bool> TryLoadDiskAsync()
        {
            try
            {
                var path = CachePath;
                if (!File.Exists(path)) return false;

                var json = await File.ReadAllTextAsync(path);
                var wrapper = JsonConvert.DeserializeObject<CacheWrapper>(json);
                if (wrapper == null || DateTime.UtcNow - wrapper.SavedAt > TTL) return false;

                _mem = wrapper.Data.ToDictionary(
                    k => k.Key,
                    k => k.Value > 0
                        ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(k.Value).UtcDateTime
                        : null);

                _loadedAt = DateTime.UtcNow;
                _initialized = true;
                Debug.WriteLine($"[LastPlayedCache] Disk hit — {_mem.Count} entries.");
                return true;
            }
            catch { return false; }
        }

        private static async Task LoadVdfAsync()
        {
            try
            {
                var steamPath = Microsoft.Win32.Registry.LocalMachine
                    .OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                    ?.GetValue("InstallPath") as string;
                if (string.IsNullOrEmpty(steamPath)) return;

                uint accountId;
                try { accountId = SteamUser.GetSteamID().GetAccountID().m_AccountID; }
                catch { return; }

                var vdfPath = Path.Combine(steamPath, "userdata",
                    accountId.ToString(), "config", "localconfig.vdf");
                if (!File.Exists(vdfPath)) return;

                var content = await File.ReadAllTextAsync(vdfPath);
                var appsSection = FindVdfSection(content, "apps");
                if (string.IsNullOrEmpty(appsSection)) return;

                var result = new Dictionary<int, DateTime?>();
                var appRegex = new Regex(
                    @"""(\d+)""\s*\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}",
                    RegexOptions.Singleline);

                foreach (Match m in appRegex.Matches(appsSection))
                {
                    if (!int.TryParse(m.Groups[1].Value, out int appId)) continue;
                    var lp = Regex.Match(m.Groups[2].Value, @"""LastPlayed""\s+""(\d+)""");
                    result[appId] = lp.Success && long.TryParse(lp.Groups[1].Value, out long ts) && ts > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime
                        : null;
                }

                _mem = result;
                _loadedAt = DateTime.UtcNow;
                _initialized = true;
                await SaveDiskAsync();
                Debug.WriteLine($"[LastPlayedCache] VDF loaded — {result.Count} entries.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LastPlayedCache] VDF error: {ex.Message}");
            }
        }

        private static async Task SaveDiskAsync()
        {
            try
            {
                var path = CachePath;
                AppPaths.Ensure(Path.GetDirectoryName(path)!);

                var raw = _mem.ToDictionary(
                    k => k.Key,
                    k => k.Value.HasValue
                        ? new DateTimeOffset(k.Value.Value).ToUnixTimeSeconds()
                        : 0L);

                var wrapper = new CacheWrapper { SavedAt = DateTime.UtcNow, Data = raw };
                await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(wrapper));
            }
            catch { }
        }

        private static string FindVdfSection(string content, string key)
        {
            var searchKey = $"\"{key}\"";
            int idx = 0;
            while (idx < content.Length)
            {
                int found = content.IndexOf(searchKey, idx, StringComparison.OrdinalIgnoreCase);
                if (found < 0) return string.Empty;

                int j = found + searchKey.Length;
                while (j < content.Length &&
                       (content[j] == ' ' || content[j] == '\t' ||
                        content[j] == '\r' || content[j] == '\n')) j++;

                if (j < content.Length && content[j] == '{')
                {
                    int depth = 1, i = j + 1;
                    while (i < content.Length && depth > 0)
                    {
                        if (content[i] == '{') depth++;
                        else if (content[i] == '}') depth--;
                        i++;
                    }
                    return depth == 0
                        ? content.Substring(j + 1, i - j - 2)
                        : string.Empty;
                }
                idx = found + 1;
            }
            return string.Empty;
        }
    }
}