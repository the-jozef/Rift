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
        public static readonly TimeSpan GameInfoTTL = TimeSpan.FromHours(24);
        public static readonly TimeSpan ListTTL = TimeSpan.FromHours(6);

        public const string KeyFeatured = "featured";
        public const string KeyDiscounts = "discounts";
        public const string KeyRecommended = "recommended";
        public const string KeyMore = "more";

        public static string KeyByTag(string tag) =>
            "tag_" + string.Concat(tag.Split(Path.GetInvalidFileNameChars()));

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private class CacheEntry<T>
        {
            public DateTime SavedAt { get; set; }
            public T? Data { get; set; }
        }

        // ─── GAME INFO ─────────────────────────────────────────────────────
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

                RestoreLocalPaths(appId, entry.Data);
                return entry.Data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] LoadGame {appId}: {ex.Message}");
                return null;
            }
        }

        public static async Task SaveGameAsync(GameModel game)
        {
            try
            {
                EnsureGameFolder(game.AppId);
                await EnsureImagesAsync(game);

                var entry = new CacheEntry<GameModel>
                {
                    SavedAt = DateTime.UtcNow,
                    Data = game
                };
                await File.WriteAllTextAsync(GetInfoPath(game.AppId),
                    JsonConvert.SerializeObject(entry, Formatting.None));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] SaveGame {game.AppId}: {ex.Message}");
            }
        }

        // ─── IMAGES ─────────────────────────────────────────────────────
        public static async Task EnsureImagesAsync(GameModel game)
        {
            EnsureGameFolder(game.AppId);
            await EnsureSingleImageAsync(game.HeaderImageUrl, GetHeaderPath(game.AppId));

            var shots = game.Screenshots ?? new List<string>();
            for (int i = 0; i < Math.Min(shots.Count, 4); i++)
                if (!string.IsNullOrEmpty(shots[i]))
                    await EnsureSingleImageAsync(shots[i], GetScreenshotPath(game.AppId, i));
        }

        public static string? GetLocalHeaderPath(int appId)
        {
            var path = GetHeaderPath(appId);
            return File.Exists(path) ? path : null;
        }

        // ─── SECTION LISTS ─────────────────────────────────────────────────────
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

        public static async Task SaveListAsync(string key, List<int> appIds)
        {
            try
            {
                AppPaths.Ensure(AppPaths.StoreLists);
                var entry = new CacheEntry<List<int>> { SavedAt = DateTime.UtcNow, Data = appIds };
                await File.WriteAllTextAsync(GetListPath(key),
                    JsonConvert.SerializeObject(entry, Formatting.None));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StoreCache] SaveList {key}: {ex.Message}");
            }
        }

        // ─── FULL SECTION HELPERS ─────────────────────────────────────────────────────
        public static async Task<List<GameModel>?> LoadSectionAsync(string listKey)
        {
            var appIds = await LoadListAsync(listKey);
            if (appIds == null) return null;

            var result = new List<GameModel>();
            foreach (var id in appIds)
            {
                var game = await LoadGameAsync(id);
                if (game != null) result.Add(game);
            }

            // Treat as cache miss if more than half the games are missing
            if (result.Count < appIds.Count / 2) return null;

            return result;
        }

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

        // ─── INVALIDATE / CLEAR ─────────────────────────────────────────────────────
        public static void InvalidateList(string key)
        {
            try
            {
                var path = GetListPath(key);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        public static void InvalidateGame(int appId)
        {
            try
            {
                var path = GetInfoPath(appId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        public static void ClearAll()
        {
            try
            {
                if (Directory.Exists(AppPaths.StoreRoot))
                    Directory.Delete(AppPaths.StoreRoot, recursive: true);
            }
            catch { }
        }

        // ─── PRIVATE HELPERS ─────────────────────────────────────────────────────
        private static async Task EnsureSingleImageAsync(string url, string localPath)
        {
            if (string.IsNullOrEmpty(url) || File.Exists(localPath)) return;
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

        // ─── Path helpers ──────────────────────────────────────────────────
        private static string GetInfoPath(int appId) => Path.Combine(AppPaths.StoreGameFolder(appId), "info.json");
        private static string GetHeaderPath(int appId) => Path.Combine(AppPaths.StoreGameFolder(appId), "header.jpg");
        private static string GetScreenshotPath(int appId, int index) => Path.Combine(AppPaths.StoreGameFolder(appId), $"ss_{index}.jpg");
        private static string GetListPath(string key) => Path.Combine(AppPaths.StoreLists, $"{key}.json");

        private static void EnsureGameFolder(int appId) =>
            AppPaths.Ensure(AppPaths.StoreGameFolder(appId));
    }
}