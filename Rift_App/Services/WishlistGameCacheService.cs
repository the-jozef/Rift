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
    /// AppData\RiftApp\wishlist\games\{appId}.json
    /// </summary>
    public static class WishlistGameCacheService
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "wishlist", "games");

        private static string FilePath(int appId) =>
            Path.Combine(Folder, $"{appId}.json");

        public static async Task<WishlistGameModel?> LoadAsync(int appId)
        {
            try
            {
                var path = FilePath(appId);
                if (!File.Exists(path)) return null;
                var json = await File.ReadAllTextAsync(path);
                var result = JsonConvert.DeserializeObject<WishlistGameModel>(json);
                Debug.WriteLine($"[WishlistCache] Loaded: {result?.Name} ({appId})");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WishlistCache] Load error {appId}: {ex.Message}");
                return null;
            }
        }

        public static async Task SaveAsync(WishlistGameModel game)
        {
            try
            {
                EnsureFolder();
                var json = JsonConvert.SerializeObject(game, Formatting.None);
                await File.WriteAllTextAsync(FilePath(game.AppId), json);
                Debug.WriteLine($"[WishlistCache] Saved: {game.Name} ({game.AppId})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WishlistCache] Save error {game.AppId}: {ex.Message}");
            }
        }

        public static void Delete(int appId)
        {
            try
            {
                var path = FilePath(appId);
                if (File.Exists(path)) File.Delete(path);
                Debug.WriteLine($"[WishlistCache] Deleted: {appId}");
            }
            catch { }
        }

        public static bool Exists(int appId) => File.Exists(FilePath(appId));

        private static void EnsureFolder()
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
                Debug.WriteLine($"[WishlistCache] Created folder: {Folder}");
            }
        }
    }
}