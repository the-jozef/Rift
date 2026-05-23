using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    public static class LocalCacheService
    {
        private static readonly string CacheFolder = AppPaths.SharedCache;

        // ─── TTL ──────────────────────────────────────────────────────────
        public static readonly TimeSpan StoreTTL = TimeSpan.FromHours(6);
        public static readonly TimeSpan LibraryTTL = TimeSpan.FromHours(24);
        public static readonly TimeSpan WishlistTTL = TimeSpan.FromHours(24);
        public static readonly TimeSpan AccountTTL = TimeSpan.FromHours(24);

        // ─── KEYS ─────────────────────────────────────────────────────────
        public const string KeyFeatured = "store_featured";
        public const string KeyTrending = "store_trending";
        public const string KeyTopSellers = "store_topsellers";
        public const string KeySpecials = "store_specials";
        public const string KeyLibrary = "library_{0}";   // {0} = steamId64
        public const string KeyWishlist = "wishlist_{0}";  // {0} = steamId64
        public const string KeyPlayer = "player_{0}";    // {0} = steamId64
        public const string KeyTags = "steam_tags";

        // ─── WRAPPER ──────────────────────────────────────────────────────
        private class CacheEntry<T>
        {
            public DateTime SavedAt { get; set; }
            public T? Data { get; set; }
        }

        // ─── SAVE ─────────────────────────────────────────────────────────
        public static async Task SaveAsync<T>(string key, T data)
        {
            try
            {
                AppPaths.Ensure(CacheFolder);
                var entry = new CacheEntry<T> { SavedAt = DateTime.UtcNow, Data = data };
                var json = JsonConvert.SerializeObject(entry, Formatting.None);
                await File.WriteAllTextAsync(GetPath(key), json);
            }
            catch { }
        }

        // ─── LOAD ─────────────────────────────────────────────────────────
        public static async Task<T?> LoadAsync<T>(string key, TimeSpan ttl)
        {
            try
            {
                var path = GetPath(key);
                if (!File.Exists(path)) return default;

                var json = await File.ReadAllTextAsync(path);
                var entry = JsonConvert.DeserializeObject<CacheEntry<T>>(json);
                if (entry == null) return default;

                if (DateTime.UtcNow - entry.SavedAt > ttl)
                {
                    File.Delete(path);
                    return default;
                }

                return entry.Data;
            }
            catch { return default; }
        }

        public static void Invalidate(string key)
        {
            try
            {
                var path = GetPath(key);
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        // ─── HELPERS ──────────────────────────────────────────────────────
        private static string GetPath(string key) =>
            Path.Combine(CacheFolder, $"{SanitizeKey(key)}.json");

        private static string SanitizeKey(string key) =>
            string.Concat(key.Split(Path.GetInvalidFileNameChars()));
    }
}