using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Rift_App.Models;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;

namespace Rift_App.Services
{
    public static class StoreGameCacheService
    {
        // ─── ROOT PATHS ───────────────────────────────────────────────────

        private static readonly string StoreRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "store");

        private static readonly string GamesRoot =
            Path.Combine(StoreRoot, "games");

        private static readonly string ListsRoot =
            Path.Combine(StoreRoot, "lists");

        // ─── TTL ──────────────────────────────────────────────────────────

        public static readonly TimeSpan GameInfoTTL = TimeSpan.FromHours(24);
        public static readonly TimeSpan ListTTL = TimeSpan.FromHours(6);

        // ─── LIST KEYS ────────────────────────────────────────────────────

        public const string KeyFeatured = "featured";
        public const string KeyDiscounts = "discounts";
        public const string KeyRecommended = "recommended";
        public const string KeyMore = "more";

        public static string KeyByTag(string tag) =>
            "tag_" + string.Concat(tag.Split(Path.GetInvalidFileNameChars()));

        // ─── HTTP ─────────────────────────────────────────────────────────

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        // ─── WRAPPER ──────────────────────────────────────────────────────

        private class CacheEntry<T>
        {
            public DateTime SavedAt { get; set; }
            public T? Data { get; set; }
        }

        // ═════════════════════════════════════════════════════════════════
        //  GAME INFO  (single game data — saved once per appId)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load a single game's cached data. Returns null if missing or expired.
        /// Does NOT load images — call EnsureImagesAsync separately.
        /// </summary>
        public static async Task<GameModel?> LoadGameAsync(int appId)
        {
            try
            {
                var path = GetInfoPath(appId);
                if (!File.Exists(path)) return null;

                var json = await File.ReadAllTextAsync(path);
                var entry = JsonConvert.DeserializeObject<CacheEntry<GameModel>>(json);
                if (entry?.Data == null) return null;

                if (DateTime.UtcNow - entry.SavedAt > GameInfoTTL)
                {
                    File.Delete(path);
                    return null;
                }

                // Restore local image paths at runtime (not stored in JSON)
                RestoreLocalPaths(appId, entry.Data);
                return entry.Data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] LoadGame {appId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save a single game's data. Automatically downloads and caches images.
        /// Safe to call multiple times — images are only downloaded once.
        /// </summary>
        public static async Task SaveGameAsync(GameModel game)
        {
            try
            {
                EnsureGameFolder(game.AppId);

                // Download images first (no-op if already on disk)
                await EnsureImagesAsync(game);

                // Save info JSON (without runtime-only fields)
                var entry = new CacheEntry<GameModel>
                {
                    SavedAt = DateTime.UtcNow,
                    Data = game
                };
                var json = JsonConvert.SerializeObject(entry, Formatting.None);
                await File.WriteAllTextAsync(GetInfoPath(game.AppId), json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] SaveGame {game.AppId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns true if game info is cached and still valid (within TTL).
        /// Fast check — does not deserialize JSON.
        /// </summary>
        public static bool IsGameCached(int appId)
        {
            try
            {
                var path = GetInfoPath(appId);
                if (!File.Exists(path)) return false;
                return DateTime.UtcNow - new FileInfo(path).LastWriteTimeUtc < GameInfoTTL;
            }
            catch { return false; }
        }

        // ═════════════════════════════════════════════════════════════════
        //  IMAGES  (header + up to 4 screenshots — stored per appId)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Ensures header image and up to 4 screenshots are on disk.
        /// Each image is downloaded only once regardless of how many sections
        /// reference this game. Returns local file paths via game properties.
        /// </summary>
        public static async Task EnsureImagesAsync(GameModel game)
        {
            EnsureGameFolder(game.AppId);

            // Header image
            await EnsureSingleImageAsync(
                game.HeaderImageUrl,
                GetHeaderPath(game.AppId));

            // Screenshots — first 4 only
            var shots = game.Screenshots ?? new List<string>();
            for (int i = 0; i < Math.Min(shots.Count, 4); i++)
            {
                if (!string.IsNullOrEmpty(shots[i]))
                    await EnsureSingleImageAsync(shots[i], GetScreenshotPath(game.AppId, i));
            }
        }

        /// <summary>
        /// Returns the local header image path if it exists, otherwise null.
        /// </summary>
        public static string? GetLocalHeaderPath(int appId)
        {
            var path = GetHeaderPath(appId);
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// Returns the local screenshot path if it exists, otherwise null.
        /// </summary>
        public static string? GetLocalScreenshotPath(int appId, int index)
        {
            var path = GetScreenshotPath(appId, index);
            return File.Exists(path) ? path : null;
        }

        // ═════════════════════════════════════════════════════════════════
        //  SECTION LISTS  (which appIds belong to a section)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load a section list (list of appIds). Returns null if missing or expired.
        /// </summary>
        public static async Task<List<int>?> LoadListAsync(string key)
        {
            try
            {
                var path = GetListPath(key);
                if (!File.Exists(path)) return null;

                var json = await File.ReadAllTextAsync(path);
                var entry = JsonConvert.DeserializeObject<CacheEntry<List<int>>>(json);
                if (entry?.Data == null) return null;

                if (DateTime.UtcNow - entry.SavedAt > ListTTL)
                {
                    File.Delete(path);
                    return null;
                }

                return entry.Data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] LoadList {key}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save a section list (list of appIds).
        /// </summary>
        public static async Task SaveListAsync(string key, List<int> appIds)
        {
            try
            {
                EnsureListsFolder();
                var entry = new CacheEntry<List<int>>
                {
                    SavedAt = DateTime.UtcNow,
                    Data = appIds
                };
                var json = JsonConvert.SerializeObject(entry, Formatting.None);
                await File.WriteAllTextAsync(GetListPath(key), json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] SaveList {key}: {ex.Message}");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD FULL SECTION  (list + game data in one call)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads a full section: list of appIds → game data for each.
        /// Games missing from cache are silently skipped (caller fetches them fresh).
        /// Returns null if the list itself is expired/missing.
        /// </summary>
        public static async Task<List<GameModel>?> LoadSectionAsync(string listKey)
        {
            var appIds = await LoadListAsync(listKey);
            if (appIds == null) return null;

            var result = new List<GameModel>();
            foreach (var id in appIds)
            {
                var game = await LoadGameAsync(id);
                if (game != null)
                    result.Add(game);
            }

            // If we lost too many games (expired individually), treat as cache miss
            if (result.Count < appIds.Count / 2)
                return null;

            return result;
        }

        /// <summary>
        /// Save a full section: saves each game individually (no duplicates)
        /// and then saves the list of appIds for this section.
        /// </summary>
        public static async Task SaveSectionAsync(string listKey, List<GameModel> games)
        {
            var appIds = new List<int>();
            foreach (var game in games)
            {
                await SaveGameAsync(game);
                appIds.Add(game.AppId);
            }
            await SaveListAsync(listKey, appIds);
        }

        // ═════════════════════════════════════════════════════════════════
        //  INVALIDATE / CLEAR
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Invalidate just the section list (force re-fetch on next load).
        /// Game data stays cached — only the list is deleted.
        /// </summary>
        public static void InvalidateList(string key)
        {
            try
            {
                var path = GetListPath(key);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        /// <summary>
        /// Invalidate a single game's cached data (not images).
        /// </summary>
        public static void InvalidateGame(int appId)
        {
            try
            {
                var path = GetInfoPath(appId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        /// <summary>
        /// Delete ALL store cache (games + lists + images).
        /// </summary>
        public static void ClearAll()
        {
            try
            {
                if (Directory.Exists(StoreRoot))
                    Directory.Delete(StoreRoot, recursive: true);
            }
            catch { }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ═════════════════════════════════════════════════════════════════

        private static async Task EnsureSingleImageAsync(string url, string localPath)
        {
            if (string.IsNullOrEmpty(url)) return;

            // Already on disk — skip download
            if (File.Exists(localPath)) return;

            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localPath, bytes);
                Debug.WriteLine($"[StoreCache] Downloaded: {Path.GetFileName(localPath)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] Image download failed ({url}): {ex.Message}");
            }
        }

        /// <summary>
        /// After loading from JSON, set runtime-only local paths so the UI
        /// can use local files instead of URLs (faster, works offline).
        /// </summary>
        private static void RestoreLocalPaths(int appId, GameModel game)
        {
            var headerPath = GetHeaderPath(appId);
            if (File.Exists(headerPath))
                game.HeaderImageUrl = headerPath;

            if (game.Screenshots == null) return;
            for (int i = 0; i < Math.Min(game.Screenshots.Count, 4); i++)
            {
                var ssPath = GetScreenshotPath(appId, i);
                if (File.Exists(ssPath))
                    game.Screenshots[i] = ssPath;
            }
        }

        // ─── PATH HELPERS ─────────────────────────────────────────────────

        private static string GetGameFolder(int appId) =>
            Path.Combine(GamesRoot, appId.ToString());

        private static string GetInfoPath(int appId) =>
            Path.Combine(GetGameFolder(appId), "info.json");

        private static string GetHeaderPath(int appId) =>
            Path.Combine(GetGameFolder(appId), "header.jpg");

        private static string GetScreenshotPath(int appId, int index) =>
            Path.Combine(GetGameFolder(appId), $"ss_{index}.jpg");

        private static string GetListPath(string key) =>
            Path.Combine(ListsRoot, $"{key}.json");

        // ─── FOLDER CREATION ──────────────────────────────────────────────

        private static void EnsureGameFolder(int appId)
        {
            var folder = GetGameFolder(appId);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private static void EnsureListsFolder()
        {
            if (!Directory.Exists(ListsRoot))
                Directory.CreateDirectory(ListsRoot);
        }
    }
}