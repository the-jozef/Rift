using System;
using System.Collections.Generic;
using System.Text;
using Rift_App.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    public static class WishlistCountCache
    {
        private static int _count = 0;
        private static bool _loaded = false;
        private static DateTime _loadedAt = DateTime.MinValue;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private static readonly TimeSpan TTL = TimeSpan.FromMinutes(10);

        public static int Count => _count;
        public static bool IsLoaded => _loaded;

        // Zavolaj raz — ďalšie volania vracajú cached hodnotu
        public static async Task<int> GetAsync()
        {
            // Cache hit
            if (_loaded && DateTime.UtcNow - _loadedAt < TTL)
                return _count;

            // Len jeden request naraz
            if (!await _lock.WaitAsync(0))
            {
                // Iný task už fetchuje — počkaj na neho max 8s
                await _lock.WaitAsync(TimeSpan.FromSeconds(8));
                _lock.Release();
                return _count;
            }

            try
            {
                // Double-check po získaní locku
                if (_loaded && DateTime.UtcNow - _loadedAt < TTL)
                    return _count;

                var steamId = SessionManager.SteamId64;
                if (string.IsNullOrEmpty(steamId)) return 0;

                var refs = await ApiService.GetWishlistIdsAsync(steamId);
                _count = refs?.Count ?? 0;
                _loaded = true;
                _loadedAt = DateTime.UtcNow;
                return _count;
            }
            finally
            {
                _lock.Release();
            }
        }

        // Zavolaj keď hráč pridá/odoberie hru z wishlistu
        public static void Invalidate()
        {
            _loaded = false;
        }

        public static void Set(int count)
        {
            _count = count;
            _loaded = true;
            _loadedAt = DateTime.UtcNow;
        }
    }
}