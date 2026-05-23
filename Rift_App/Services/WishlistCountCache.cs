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
        private static string _steamId = string.Empty;  
        private static DateTime _loadedAt = DateTime.MinValue;
        private static readonly SemaphoreSlim _lock = new(1, 1);
        private static readonly TimeSpan TTL = TimeSpan.FromMinutes(10);

        public static int Count => _count;
        public static bool IsLoaded => _loaded;

        public static async Task<int> GetAsync()
        {
            var currentSteamId = SessionManager.SteamId64;

            // Cache hit: same account, still fresh
            if (_loaded
                && _steamId == currentSteamId
                && DateTime.UtcNow - _loadedAt < TTL)
                return _count;

            // Only one fetch at a time
            if (!await _lock.WaitAsync(0))
            {
                // Another task is already fetching — wait for it to finish
                await _lock.WaitAsync(TimeSpan.FromSeconds(8));
                _lock.Release();
                return _count;
            }

            try
            {
                // Double-check after acquiring the lock
                if (_loaded
                    && _steamId == currentSteamId
                    && DateTime.UtcNow - _loadedAt < TTL)
                    return _count;

                if (string.IsNullOrEmpty(currentSteamId))
                {
                    _count = 0;
                    _loaded = true;
                    _steamId = string.Empty;
                    _loadedAt = DateTime.UtcNow;
                    return 0;
                }

                var refs = await ApiService.GetWishlistIdsAsync(currentSteamId);
                _count = refs?.Count ?? 0;
                _loaded = true;
                _steamId = currentSteamId;
                _loadedAt = DateTime.UtcNow;
                return _count;
            }
            finally
            {
                _lock.Release();
            }
        }

        public static void Invalidate()
        {
            _loaded = false;
            _steamId = string.Empty;
        }

        public static void Set(int count)
        {
            _count = count;
            _loaded = true;
            _steamId = SessionManager.SteamId64;
            _loadedAt = DateTime.UtcNow;
        }
    }
}