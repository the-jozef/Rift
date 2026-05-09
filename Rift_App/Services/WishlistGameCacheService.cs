using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Rift_App.Models;
using System.Diagnostics;
using System.IO;

namespace Rift_App.Services
{
    /// <summary>
    /// Každá hra má vlastný JSON súbor.
    /// Cesta: AppData\RiftApp\wishlist\games\{appId}.json
    /// </summary>
    public static class WishlistGameCacheService
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "wishlist", "games");

        private static string FilePath(int appId) =>
            Path.Combine(Folder, $"{appId}.json");

        // ─── LOAD ─────────────────────────────────────────────────────────

        public static async Task<WishlistGameModel?> LoadAsync(int appId)
        {
            try
            {
                var path = FilePath(appId);
                if (!File.Exists(path)) return null;
                var json = await File.ReadAllTextAsync(path);
                return JsonConvert.DeserializeObject<WishlistGameModel>(json);
            }
            catch { return null; }
        }

        // ─── SAVE ─────────────────────────────────────────────────────────

        public static async Task SaveAsync(WishlistGameModel game)
        {
            try
            {
                EnsureFolder();
                var json = JsonConvert.SerializeObject(game, Formatting.None);
                await File.WriteAllTextAsync(FilePath(game.AppId), json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WishlistGameCache] Save error {game.AppId}: {ex.Message}");
            }
        }

        // ─── DELETE ───────────────────────────────────────────────────────

        public static void Delete(int appId)
        {
            try
            {
                var path = FilePath(appId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        // ─── EXISTS ───────────────────────────────────────────────────────

        public static bool Exists(int appId) => File.Exists(FilePath(appId));

        // ─── HELPER ───────────────────────────────────────────────────────

        private static void EnsureFolder()
        {
            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
        }
    }
}