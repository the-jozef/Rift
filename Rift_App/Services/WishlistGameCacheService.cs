using Newtonsoft.Json;
using Rift_App.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace Rift_App.Services
{
    /// <summary>
    /// AppData\RiftApp\wishlist\games\{appId}.json
    /// </summary>
    public static class WishlistGameCacheService
    {
        private static readonly string BaseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RiftApp", "wishlist", "games");

        // Každý účet má vlastnú zložku podľa steamId
        private static string UserFolder(string steamId) =>
            Path.Combine(BaseFolder, steamId);

        private static string FilePath(string steamId, int appId) =>
            Path.Combine(UserFolder(steamId), $"{appId}.json");

        public static async Task<WishlistGameModel?> LoadAsync(int appId)
        {
            var steamId = SessionManager.SteamId64;
            return await LoadAsync(steamId, appId);
        }

        public static async Task<WishlistGameModel?> LoadAsync(string steamId, int appId)
        {
            try
            {
                var path = FilePath(steamId, appId);
                if (!File.Exists(path)) return null;

                var fileAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(path);
                if (fileAge.TotalHours > 24)
                {
                    File.Delete(path);
                    return null;
                }

                var json = await File.ReadAllTextAsync(path);
                return JsonConvert.DeserializeObject<WishlistGameModel>(json);
            }
            catch { return null; }
        }

        public static async Task SaveAsync(WishlistGameModel game)
        {
            var steamId = SessionManager.SteamId64;
            await SaveAsync(steamId, game);
        }

        public static async Task SaveAsync(string steamId, WishlistGameModel game)
        {
            try
            {
                EnsureFolder(steamId);
                var json = JsonConvert.SerializeObject(game, Formatting.None);
                await File.WriteAllTextAsync(FilePath(steamId, game.AppId), json);
            }
            catch { }
        }

        public static void Delete(int appId)
        {
            var steamId = SessionManager.SteamId64;
            Delete(steamId, appId);
        }

        public static void Delete(string steamId, int appId)
        {
            try
            {
                var path = FilePath(steamId, appId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private static void EnsureFolder(string steamId)
        {
            var folder = UserFolder(steamId);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
    }
}