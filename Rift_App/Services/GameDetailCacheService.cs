using Newtonsoft.Json;
using Rift_App.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;

namespace Rift_App.Services
{
    /// <summary>
    /// Caches per-game detail JSON in AppData\RiftApp\games\{appId}.json
    /// Also downloads and caches hero images in AppData\RiftApp\game_images\
    /// TTL: 24 hours. Hero image: kept until the game is removed from library.
    /// Shared between Library and Store game pages.
    /// </summary>
    public static class GameDetailCacheService
    {
        private static readonly string GamesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "games");

        private static readonly string ImagesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "game_images");

        private static readonly TimeSpan DetailTTL = TimeSpan.FromHours(24);

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // ─── LOAD ─────────────────────────────────────────────────────────

        public static async Task<GameDetailModel?> LoadAsync(int appId)
        {
            try
            {
                var path = GetDetailPath(appId);
                if (!File.Exists(path)) return null;

                var json = await File.ReadAllTextAsync(path);
                var detail = JsonConvert.DeserializeObject<GameDetailModel>(json);
                if (detail == null) return null;

                // Expired — delete and return null
                if (DateTime.UtcNow - detail.CachedAt > DetailTTL)
                {
                    File.Delete(path);
                    return null;
                }

                // Restore local hero image path
                var heroPath = GetHeroPath(appId);
                detail.HeroImagePath = File.Exists(heroPath) ? heroPath : null;

                return detail;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameDetailCache] Load error {appId}: {ex.Message}");
                return null;
            }
        }

        // ─── SAVE ─────────────────────────────────────────────────────────

        public static async Task SaveAsync(GameDetailModel detail)
        {
            try
            {
                EnsureFolders();
                detail.CachedAt = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(detail, Formatting.None);
                await File.WriteAllTextAsync(GetDetailPath(detail.AppId), json);
                Debug.WriteLine($"[GameDetailCache] Saved {detail.AppId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameDetailCache] Save error: {ex.Message}");
            }
        }

        // ─── HERO IMAGE ───────────────────────────────────────────────────
        // Tries library_hero (1920x620) first, falls back to header (460x215)
        // Returns local file path, or null if download failed

        public static async Task<string?> EnsureHeroImageAsync(int appId)
        {
            try
            {
                EnsureFolders();
                var heroPath = GetHeroPath(appId);

                // Already on disk — return immediately
                if (File.Exists(heroPath)) return heroPath;

                // Try library hero first (best quality)
                var bytes = await TryDownloadAsync(
                    $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/library_hero.jpg");

                // Fallback to standard header image
                if (bytes == null)
                    bytes = await TryDownloadAsync(
                        $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg");

                if (bytes == null)
                {
                    Debug.WriteLine($"[GameDetailCache] No hero image for {appId}");
                    return null;
                }

                await File.WriteAllBytesAsync(heroPath, bytes);
                Debug.WriteLine($"[GameDetailCache] Hero saved for {appId}");
                return heroPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameDetailCache] Hero error {appId}: {ex.Message}");
                return null;
            }
        }

        // ─── DELETE — called when a game is removed from library ──────────

        public static void Delete(int appId)
        {
            try
            {
                var detail = GetDetailPath(appId);
                if (File.Exists(detail)) File.Delete(detail);

                var hero = GetHeroPath(appId);
                if (File.Exists(hero)) File.Delete(hero);
            }
            catch { }
        }

        // ─── HELPERS ──────────────────────────────────────────────────────

        private static async Task<byte[]?> TryDownloadAsync(string url)
        {
            try
            {
                var response = await _http.GetAsync(url);
                return response.IsSuccessStatusCode
                    ? await response.Content.ReadAsByteArrayAsync()
                    : null;
            }
            catch { return null; }
        }

        public static string GetHeroPath(int appId) =>
            Path.Combine(ImagesFolder, $"{appId}_hero.jpg");

        private static string GetDetailPath(int appId) =>
            Path.Combine(GamesFolder, $"{appId}.json");

        private static void EnsureFolders()
        {
            if (!Directory.Exists(GamesFolder)) Directory.CreateDirectory(GamesFolder);
            if (!Directory.Exists(ImagesFolder)) Directory.CreateDirectory(ImagesFolder);
        }
    }
}