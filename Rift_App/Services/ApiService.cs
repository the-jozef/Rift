using Newtonsoft.Json;
using Rift_App.Languages;
using Rift_App.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Rift_App.Services
{
    public static class ApiService
    {
        private const string BaseUrl = "https://rift-hupv.onrender.com";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static StringContent ToJson(object obj) =>
            new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");

        private static T? FromJson<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json);

        // ─── RETRY helper  ───────────────────
        private static async Task<string?> GetWithRetryAsync(string url, int maxRetries = 3)
        {
            int delay = 1000;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    return await _http.GetStringAsync(url, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (i == maxRetries - 1) return null;
                    Debug.WriteLine($"[API] Timeout on {url}, retry {i + 1}");
                    await Task.Delay(delay);
                    delay *= 2;
                }
                catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    if (i == maxRetries - 1) throw;
                    Debug.WriteLine($"[API] 429 on {url}, retry {i + 1} after {delay}ms");
                    await Task.Delay(delay);
                    delay *= 2;
                }
            }
            return null;
        }

        // ─── AUTH  ─────────────────────────────────────────────────────────
        public static async Task<AuthResponse?> RegisterAsync(string username, string password, string steamId64)
        {
            try
            {
                var body = ToJson(new { Username = username, Password = password, SteamId64 = steamId64, DeviceToken = DeviceTokenService.GetOrCreate() });
                var response = await _http.PostAsync($"{BaseUrl}/api/auth/register", body);
                return FromJson<AuthResponse>(await response.Content.ReadAsStringAsync());
            }
            catch { return new AuthResponse { Success = false, Message = L.Get("err_connection_error") }; }
        }

        public static async Task<AuthResponse?> LoginRiftAsync(string username, string password)
        {
            try
            {
                var body = ToJson(new { Username = username, Password = password, DeviceToken = DeviceTokenService.GetOrCreate() });
                var response = await _http.PostAsync($"{BaseUrl}/api/auth/login", body);
                return FromJson<AuthResponse>(await response.Content.ReadAsStringAsync());
            }
            catch { return new AuthResponse { Success = false, Message = L.Get("err_connection_error") }; }
        }

        public static async Task<AuthResponse?> LoginSteamAsync(string steamId64)
        {
            try
            {
                var body = ToJson(new { SteamId64 = steamId64, DeviceToken = DeviceTokenService.GetOrCreate() });
                var response = await _http.PostAsync($"{BaseUrl}/api/auth/steam-login", body);
                return FromJson<AuthResponse>(await response.Content.ReadAsStringAsync());
            }
            catch { return new AuthResponse { Success = false, Message = L.Get("err_connection_error") }; }
        }

        // ─── DEVICE  ─────────────────────────────────────────────────────────
        public static async Task InitDeviceAsync()
        {
            try
            {
                var body = ToJson(new { DeviceToken = DeviceTokenService.GetOrCreate() });
                await _http.PostAsync($"{BaseUrl}/api/device/init", body);
            }
            catch { }
        }

        public static async Task<List<AccountInfo>> GetDeviceAccountsAsync()
        {
            try
            {
                var token = DeviceTokenService.GetOrCreate();
                var response = await _http.GetStringAsync($"{BaseUrl}/api/device/{token}/accounts");
                return FromJson<List<AccountInfo>>(response) ?? new List<AccountInfo>();
            }
            catch { return new List<AccountInfo>(); }
        }

        public static async Task<SessionResponse?> GetSessionAsync()
        {
            try
            {
                var token = DeviceTokenService.GetOrCreate();
                var response = await _http.GetStringAsync($"{BaseUrl}/api/device/{token}/session");
                return FromJson<SessionResponse>(response);
            }
            catch { return new SessionResponse { HasSession = false }; }
        }

        public static async Task SaveSessionAsync(string location)
        {
            try
            {
                if (!SessionManager.IsLoggedIn) return;
                var body = ToJson(new
                {
                    DeviceToken = DeviceTokenService.GetOrCreate(),
                    UserId = SessionManager.UserId,
                    LastLocation = location
                });
                await _http.PostAsync($"{BaseUrl}/api/device/session", body);
                SessionManager.UpdateLocation(location);
            }
            catch { }
        }

        public static async Task<bool> RemoveAccountFromDeviceAsync(Guid userId)
        {
            try
            {
                var token = DeviceTokenService.GetOrCreate();
                var response = await _http.DeleteAsync($"{BaseUrl}/api/device/{token}/account/{userId}");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> DeleteAccountFullyAsync(Guid userId)
        {
            try
            {
                var response = await _http.DeleteAsync($"{BaseUrl}/api/device/account/{userId}/full");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ─── STEAM — PLAYER  ─────────────────────────────────────────────────────────
        public static async Task<PlayerInfo?> GetPlayerInfoAsync(string steamId64)
        {
            try
            {
                var response = await GetWithRetryAsync($"{BaseUrl}/api/steam/player/{steamId64}");
                return response == null ? null : FromJson<PlayerInfo>(response);
            }
            catch { return null; }
        }

        public static async Task<int> GetSteamLevelAsync(string steamId64)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/player/{steamId64}/level");
                return response == null ? 0 : FromJson<LevelResponse>(response)?.Level ?? 0;
            }
            catch { return 0; }
        }

        public static async Task<FriendsResponse?> GetFriendsAsync(string steamId64)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/player/{steamId64}/friends");
                return response == null ? null : FromJson<FriendsResponse>(response);
            }
            catch { return null; }
        }

        public static async Task<RecentActivityGame.RecentActivityResponse> GetRecentActivityAsync(string steamId64)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/player/{steamId64}/recentactivity");
                return response == null ? null : FromJson<RecentActivityGame.RecentActivityResponse>(response);
            }
            catch { return null; }
        }

        public static async Task<List<SearchResultModel>> SearchGamesAsync(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query)) return new();
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/store/search?q={Uri.EscapeDataString(query)}");
                return response == null ? new() : FromJson<SearchResponse>(response)?.Results ?? new();
            }
            catch { return new(); }
        }

        // ─── STEAM — LIBRARY  ─────────────────────────────────────────────────────────
        public static async Task<List<GameModel>> GetLibraryAsync(string steamId64)
        {
            try
            {
                var response = await GetWithRetryAsync($"{BaseUrl}/api/steam/library/{steamId64}");
                return response == null ? new() : FromJson<GamesResponse>(response)?.Games ?? new();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<GameInfoModel?> GetLibraryGameInfoAsync(int appId)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/library/game/{appId}/info");
                return response == null ? null : FromJson<GameInfoModel>(response);
            }
            catch { return null; }
        }

        public static async Task<List<GameModel>> GetFullLibraryAsync(string steamId64)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/library/{steamId64}/full");
                return response == null ? new() : FromJson<GamesResponse>(response)?.Games ?? new();
            }
            catch { return new List<GameModel>(); }
        }

        // ─── STEAM — ACHIEVEMENTS  ─────────────────────────────────────────────────────────
        public static async Task<GameDetailModel?> GetAchievementsAsync(int appId, string steamId64)
        {
            try
            {
                var json = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/achievements/{appId}/{steamId64}");
                if (json == null) return null;

                var data = JsonConvert.DeserializeObject<AchievementsResponse>(json);
                if (data == null) return null;

                Debug.WriteLine($"[API] Achievements: {data.Total}, Unlocked: {data.Unlocked}");
                return new GameDetailModel
                {
                    AppId = appId,
                    Achievements = data.Achievements ?? new List<AchievementModel>(),
                    AchievementsTotal = data.Total,
                    AchievementsUnlocked = data.Unlocked
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] GetAchievements error: {ex.Message}");
                return null;
            }
        }

        private class AchievementsResponse
        {
            public List<AchievementModel>? Achievements { get; set; }
            public int Total { get; set; }
            public int Unlocked { get; set; }
        }

        // ─── STEAM — STORE  ─────────────────────────────────────────────────────────
        public static async Task<List<GameModel>> GetFeaturedAsync()
        {
            try
            {
                var response = await GetWithRetryAsync($"{BaseUrl}/api/steam/store/featured");
                return response == null ? new() : FromJson<GamesResponse>(response)?.Games ?? new();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<List<GameModel>> GetDiscountsAsync()
        {
            try
            {
                var response = await GetWithRetryAsync($"{BaseUrl}/api/steam/store/discounts");
                return response == null ? new() : FromJson<GamesResponse>(response)?.Games ?? new();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<List<GameModel>> GetRecommendedAsync(IEnumerable<string> genres)
        {
            try
            {
                var genreParam = Uri.EscapeDataString(string.Join(",", genres));
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/store/recommended?genres={genreParam}");
                return response == null ? new() : FromJson<GamesResponse>(response)?.Games ?? new();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<List<GameModel>> GetByTagAsync(string tag)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/store/bytag?tag={Uri.EscapeDataString(tag)}");
                return response == null ? new() : FromJson<GamesResponse>(response)?.Games ?? new();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<MoreGamesResponse> GetMoreAsync(int page)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/store/more?page={page}");
                return response == null
                    ? new MoreGamesResponse { Games = new(), HasMore = false }
                    : FromJson<MoreGamesResponse>(response)
                      ?? new MoreGamesResponse { Games = new(), HasMore = false };
            }
            catch { return new MoreGamesResponse { Games = new(), HasMore = false }; }
        }

        public static async Task<List<GameModel>> GetNewTrendingAsync(int page = 0)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/store/newtrending?page={page}");
                return response == null ? new() : FromJson<GamesResponse>(response)?.Games ?? new();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<List<GameModel>> GetTopSellersAsync(int page = 0)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/store/topsellers?page={page}");
                return response == null ? new() : FromJson<GamesResponse>(response)?.Games ?? new();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<List<GameModel>> GetSpecialsAsync(int page = 0)
        {
            try
            {
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/store/specials?page={page}");
                return response == null ? new() : FromJson<GamesResponse>(response)?.Games ?? new();
            }
            catch { return new List<GameModel>(); }
        }

        // ─── STEAM — SINGLE GAME  ─────────────────────────────────────────────────────────
        public static async Task<GameModel?> GetGameDetailsAsync(int appId)
        {
            try
            {
                var response = await GetWithRetryAsync($"{BaseUrl}/api/steam/game/{appId}");
                return response == null ? null : FromJson<GameModel>(response);
            }
            catch { return null; }
        }

        // ─── WISHLIST  ─────────────────────────────────────────────────────────
        public static async Task<List<WishlistItemRef>> GetWishlistIdsAsync(string steamId64)
        {
            try
            {
                Debug.WriteLine($"[API] GetWishlistIds: {steamId64}");
                var response = await GetWithRetryAsync(
                    $"{BaseUrl}/api/steam/wishlist/{steamId64}/ids");
                var result = response == null
                    ? new List<WishlistItemRef>()
                    : FromJson<WishlistIdsResponse>(response)?.Items ?? new();
                Debug.WriteLine($"[API] GetWishlistIds: {result.Count} items");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] GetWishlistIds error: {ex.Message}");
                return new List<WishlistItemRef>();
            }
        }

        public static async Task<List<WishlistGameModel>> GetWishlistBatchAsync(List<int> appIds)
        {
            try
            {
                Debug.WriteLine($"[API] GetWishlistBatch: {appIds.Count} ids");
                var body = ToJson(new { AppIds = appIds });
                var response = await _http.PostAsync($"{BaseUrl}/api/steam/wishlist/batch", body);
                var json = await response.Content.ReadAsStringAsync();
                var result = FromJson<WishlistGamesResponse>(json)?.Games ?? new();
                Debug.WriteLine($"[API] GetWishlistBatch parsed: {result.Count} games");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] GetWishlistBatch error: {ex.Message}");
                return new List<WishlistGameModel>();
            }
        }

        public static async Task<bool> RemoveFromWishlistAsync(string steamId64, int appId)
        {
            try
            {
                var response = await _http.PostAsync(
                    $"{BaseUrl}/api/steam/wishlist/remove/{steamId64}/{appId}", null);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ─── RESPONSE TYPES  ─────────────────────────────────────────────────────────
        public class AuthResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public Guid? UserId { get; set; }
            public string? Username { get; set; }
            public string? SteamId64 { get; set; }
        }

        public class SessionResponse
        {
            public bool HasSession { get; set; }
            public Guid? UserId { get; set; }
            public string? Username { get; set; }
            public string? SteamId64 { get; set; }
            public string LastLocation { get; set; } = "Store";
        }

        public class GamesResponse
        {
            public List<GameModel> Games { get; set; } = new();
        }

        public class MoreGamesResponse
        {
            public List<GameModel> Games { get; set; } = new();
            public bool HasMore { get; set; }
            public int Page { get; set; }
        }

        public class WishlistGamesResponse
        {
            public List<WishlistGameModel> Games { get; set; } = new();
        }

        public class WishlistIdsResponse
        {
            public List<WishlistItemRef> Items { get; set; } = new();
        }

        public class LevelResponse
        {
            public int Level { get; set; }
        }
    }
}