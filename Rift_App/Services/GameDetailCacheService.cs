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
    public static class GameDetailCacheService
    {
        private static readonly TimeSpan DetailTTL = TimeSpan.FromHours(24);

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // ─── PATHS ────────────────────────────────────────────────────────

        private static string DetailsFolder =>
            AppPaths.Ensure(AppPaths.GameDetails(SessionManager.SteamId64));

        private static string DetailPath(int appId) =>
            Path.Combine(DetailsFolder, $"{appId}.json");

        public static string GetHeroPath(int appId) =>
            Path.Combine(AppPaths.Ensure(AppPaths.GameHeroImages), $"{appId}_hero.jpg");

        // ─── LOAD ─────────────────────────────────────────────────────────

        public static async Task<GameDetailModel?> LoadAsync(int appId)
        {
            try
            {
                var path = DetailPath(appId);
                if (!File.Exists(path)) return null;

                var json = await File.ReadAllTextAsync(path);
                var detail = JsonConvert.DeserializeObject<GameDetailModel>(json);
                if (detail == null) return null;

                if (DateTime.UtcNow - detail.CachedAt > DetailTTL)
                {
                    File.Delete(path);
                    return null;
                }

                // Restore hero path from shared folder
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
                detail.CachedAt = DateTime.UtcNow;
                var json = JsonConvert.SerializeObject(detail, Formatting.None);
                await File.WriteAllTextAsync(DetailPath(detail.AppId), json);
                Debug.WriteLine($"[GameDetailCache] Saved {detail.AppId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GameDetailCache] Save error: {ex.Message}");
            }
        }

        // ─── HERO IMAGE (shared — same for every user) ────────────────────

        public static async Task<string?> EnsureHeroImageAsync(int appId)
        {
            try
            {
                var heroPath = GetHeroPath(appId);
                if (File.Exists(heroPath)) return heroPath;

                // Try library_hero first, fall back to header.jpg
                var bytes = await TryDownloadAsync(
                    $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/library_hero.jpg");
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

        // ─── DELETE ───────────────────────────────────────────────────────

        public static void Delete(int appId)
        {
            try
            {
                var detail = DetailPath(appId);
                if (File.Exists(detail)) File.Delete(detail);
            }
            catch { }
        }

        // ─── HELPER ───────────────────────────────────────────────────────

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
    }
}