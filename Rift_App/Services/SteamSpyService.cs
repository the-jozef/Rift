using Newtonsoft.Json;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    public static class SteamSpyService
    {
        private static readonly string CachePath =
            Path.Combine(AppPaths.SharedCache, "steamspy_f2p.json");

        private static readonly TimeSpan TTL = TimeSpan.FromDays(7);

        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static Dictionary<int, string>? _cache;

        public static async Task<Dictionary<int, string>> GetFreeGamesAsync()
        {
            if (_cache != null) return _cache;

            _cache = await TryLoadDiskAsync();
            if (_cache != null) return _cache;

            _cache = await DownloadAsync();
            return _cache ?? new();
        }

        // ─── PRIVATE ──────────────────────────────────────────────────────

        private static async Task<Dictionary<int, string>?> TryLoadDiskAsync()
        {
            try
            {
                AppPaths.Ensure(AppPaths.SharedCache);
                if (!File.Exists(CachePath)) return null;

                var info = new FileInfo(CachePath);
                if (DateTime.UtcNow - info.LastWriteTimeUtc > TTL) return null;

                var json = await File.ReadAllTextAsync(CachePath);
                var result = JsonConvert.DeserializeObject<Dictionary<int, string>>(json);
                Debug.WriteLine($"[SteamSpy] Disk cache hit — {result?.Count} F2P games.");
                if (result?.Count < 10000)
                {
                    File.Delete(CachePath);
                    return null;
                }
                return result;
            }
            catch { return null; }
        }

        private static readonly string[] SteamSpyGenres =
        {
            "Free%20to%20Play",
            "Early%20Access",
            "Massively%20Multiplayer",
        };

        private static async Task<Dictionary<int, string>?> DownloadAsync()
        {
            try
            {
                var result = new Dictionary<int, string>();

                foreach (var genre in SteamSpyGenres)
                {
                    try
                    {
                        var json = await _http.GetStringAsync(
                            $"https://steamspy.com/api.php?request=genre&genre={genre}");
                        var raw = JsonConvert.DeserializeObject<Dictionary<string, SteamSpyEntry>>(json);
                        if (raw == null) continue;

                        foreach (var kvp in raw)
                        {
                            if (int.TryParse(kvp.Key, out int appId) && appId > 0
                                && !string.IsNullOrEmpty(kvp.Value.Name))
                                result[appId] = kvp.Value.Name;
                        }

                        Debug.WriteLine($"[SteamSpy] Genre '{genre}': {raw.Count} games");
                        await Task.Delay(1000);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[SteamSpy] Genre '{genre}' error: {ex.Message}");
                    }
                }

                await SaveDiskAsync(result);
                Debug.WriteLine($"[SteamSpy] Downloaded total {result.Count} games.");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamSpy] Download error: {ex.Message}");
                return null;
            }
        }

        private static async Task SaveDiskAsync(Dictionary<int, string> data)
        {
            try
            {
                AppPaths.Ensure(AppPaths.SharedCache);
                await File.WriteAllTextAsync(CachePath,
                    JsonConvert.SerializeObject(data, Formatting.None));
            }
            catch { }
        }

        private class SteamSpyEntry
        {
            [JsonProperty("name")]
            public string Name { get; set; } = "";
        }
    }
}