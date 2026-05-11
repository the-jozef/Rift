using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Rift_App.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    /// <summary>
    /// Saves the game library (names + playtime) and icons to disk.
    /// Location: AppData\RiftApp\library\
    /// </summary>
    public static class LibraryCacheService
    {
        private static readonly string LibraryFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "library");

        private static readonly string IconFolder = Path.Combine(LibraryFolder, "icons");
        private static readonly string GamesFile = Path.Combine(LibraryFolder, "games.json");

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // ─── LOAD ─────────────────────────────────────────────────────────

        public static async Task<List<GameModel>?> LoadAsync()
        {
            try
            {
                if (!File.Exists(GamesFile)) return null;
                var json = await File.ReadAllTextAsync(GamesFile);
                return JsonConvert.DeserializeObject<List<GameModel>>(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryCache] Load error: {ex.Message}");
                return null;
            }
        }

        // ─── SAVE ─────────────────────────────────────────────────────────

        public static async Task SaveAsync(List<GameModel> games)
        {
            try
            {
                EnsureFolders();
                var json = JsonConvert.SerializeObject(games, Formatting.None);
                await File.WriteAllTextAsync(GamesFile, json);
                Debug.WriteLine($"[LibraryCache] Saved {games.Count} games.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryCache] Save error: {ex.Message}");
            }
        }

        // ─── SYNC — compare cached vs fresh API data ──────────────────────
        // Returns (updated list, wasChanged)

        public static async Task<(List<GameModel> Games, bool Changed)> SyncAsync(List<GameModel> cached,List<GameModel> freshFromApi)
        {
            bool changed = false;
            var result = new List<GameModel>(cached);

            var cachedById = cached.ToDictionary(g => g.AppId);
            var freshById = freshFromApi.ToDictionary(g => g.AppId);

            // Nové hry — sú v API ale nie v cache
            foreach (var freshGame in freshFromApi)
            {
                if (cachedById.ContainsKey(freshGame.AppId)) continue;

                Debug.WriteLine($"[LibraryCache] Nová hra: {freshGame.Name}");
                freshGame.IconPath = await DownloadIconAsync(freshGame.AppId, freshGame.IconUrl);
                result.Add(freshGame);
                changed = true;
            }

            // Update install status — NEmazaj hry, len aktualizuj IsInstalled
            foreach (var cachedGame in cached)
            {
                if (!freshById.ContainsKey(cachedGame.AppId))
                {
                    // Hra zmizla z API (veľmi zriedkavé) — nechaj ju v cache
                    continue;
                }

                var fresh = freshById[cachedGame.AppId];

                // Aktualizuj playtime
                if (fresh.PlaytimeMinutes > cachedGame.PlaytimeMinutes)
                {
                    cachedGame.PlaytimeMinutes = fresh.PlaytimeMinutes;
                    changed = true;
                }
            }

            return (result, changed);
        }

        // ─── DOWNLOAD ALL ICONS — called on first run ──────────────────────

        public static async Task DownloadAllIconsAsync(List<GameModel> games)
        {
            EnsureFolders();
            using var semaphore = new SemaphoreSlim(5, 5);

            var tasks = games.Select(async game =>
            {
                await semaphore.WaitAsync();
                try
                {
                    game.IconPath = await DownloadIconAsync(game.AppId, game.IconUrl);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        // ─── PUBLIC HELPERS ───────────────────────────────────────────────

        /// <summary>Returns the expected icon path for a given AppId.</summary>
        public static string GetIconPath(int appId) =>
            Path.Combine(IconFolder, $"{appId}.jpg");

        public static bool CacheExists() => File.Exists(GamesFile);

        public static void DeleteIcon(int appId)
        {
            try
            {
                var path = GetIconPath(appId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        // ─── PRIVATE ──────────────────────────────────────────────────────

        private static async Task<string?> DownloadIconAsync(int appId, string iconUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(iconUrl)) return null;

                EnsureFolders();
                var localPath = GetIconPath(appId);

                if (File.Exists(localPath)) return localPath;

                var bytes = await _http.GetByteArrayAsync(iconUrl);
                await File.WriteAllBytesAsync(localPath, bytes);
                Debug.WriteLine($"[LibraryCache] Icon saved: {appId}");
                return localPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryCache] Icon error {appId}: {ex.Message}");
                return null;
            }
        }

        private static void EnsureFolders()
        {
            if (!Directory.Exists(LibraryFolder)) Directory.CreateDirectory(LibraryFolder);
            if (!Directory.Exists(IconFolder)) Directory.CreateDirectory(IconFolder);
        }
    }
}