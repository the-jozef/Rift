using Newtonsoft.Json;
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
            Timeout = TimeSpan.FromSeconds(90)  
        };

        private static readonly HttpClient _wishlistHttp = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        private static StringContent ToJson(object obj) =>
            new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");

        private static T? FromJson<T>(string json) =>
            JsonConvert.DeserializeObject<T>(json);

        // ─── AUTH ─────────────────────────────────────────────────────────────

        public static async Task<AuthResponse?> RegisterAsync(string username, string password, string steamId64)
        {
            try
            {
                var body = ToJson(new { Username = username, Password = password, SteamId64 = steamId64, DeviceToken = DeviceTokenService.GetOrCreate() });
                var response = await _http.PostAsync($"{BaseUrl}/api/auth/register", body);
                return FromJson<AuthResponse>(await response.Content.ReadAsStringAsync());
            }
            catch { return new AuthResponse { Success = false, Message = "Connection error. Check your internet." }; }
        }

        public static async Task<AuthResponse?> LoginRiftAsync(string username, string password)
        {
            try
            {
                var body = ToJson(new { Username = username, Password = password, DeviceToken = DeviceTokenService.GetOrCreate() });
                var response = await _http.PostAsync($"{BaseUrl}/api/auth/login", body);
                return FromJson<AuthResponse>(await response.Content.ReadAsStringAsync());
            }
            catch { return new AuthResponse { Success = false, Message = "Connection error. Check your internet." }; }
        }

        public static async Task<AuthResponse?> LoginSteamAsync(string steamId64)
        {
            try
            {
                var body = ToJson(new { SteamId64 = steamId64, DeviceToken = DeviceTokenService.GetOrCreate() });
                var response = await _http.PostAsync($"{BaseUrl}/api/auth/steam-login", body);
                return FromJson<AuthResponse>(await response.Content.ReadAsStringAsync());
            }
            catch { return new AuthResponse { Success = false, Message = "Connection error. Check your internet." }; }
        }

        // ─── DEVICE ───────────────────────────────────────────────────────────

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

        // ─── STEAM DATA ───────────────────────────────────────────────────────

        public static async Task<PlayerInfo?> GetPlayerInfoAsync(string steamId64)
        {
            try
            {
                var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/player/{steamId64}");
                return FromJson<PlayerInfo>(response);
            }
            catch { return null; }
        }

        public static async Task<List<GameModel>> GetLibraryAsync(string steamId64)
        {
            try
            {
                var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/library/{steamId64}");
                return FromJson<GamesResponse>(response)?.Games ?? new List<GameModel>();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<GameDetailModel?> GetAchievementsAsync(int appId, string steamId64)
        {
            try
            {
                var json = await _http.GetStringAsync(
                    $"{BaseUrl}/api/steam/achievements/{appId}/{steamId64}");
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

        public static async Task<List<GameModel>> GetWishlistAsync(string steamId64)
        {
            try
            {
                var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/wishlist/{steamId64}");
                return FromJson<GamesResponse>(response)?.Games ?? new List<GameModel>();
            }
            catch { return new List<GameModel>(); }
        }


        public static async Task<List<WishlistGameModel>> GetWishlistDetailedAsync(string steamId64)
        {
            try
            {
                // Normálny timeout — backend je teraz rýchly (batch requesty)
                var response = await _http.GetStringAsync(
                    $"{BaseUrl}/api/steam/wishlist/{steamId64}");
                return FromJson<WishlistGamesResponse>(response)?.Games
                       ?? new List<WishlistGameModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] GetWishlistDetailed error: {ex.Message}");
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

        public static async Task<List<WishlistItemRef>> GetWishlistIdsAsync(string steamId64)
        {
            try
            {
                var response = await _http.GetStringAsync(
                    $"{BaseUrl}/api/steam/wishlist/{steamId64}/ids");
                return FromJson<WishlistIdsResponse>(response)?.Items
                       ?? new List<WishlistItemRef>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] GetWishlistIds error: {ex.Message}");
                return new List<WishlistItemRef>();
            }
        }


        public static async Task<WishlistGameModel?> GetWishlistGameDetailAsync(int appId, long dateAdded)
        {
            try
            {
                var response = await _wishlistHttp.GetStringAsync(
                    $"{BaseUrl}/api/steam/game/{appId}");

                var game = FromJson<GameModel>(response);
                if (game == null) return null;

                // Zisti release info priamo zo Steam store API cez backend
                // game.Price == "N/A" a game.Genres prázdne = hra ešte nevydaná
                bool isReleased = game.Price != "N/A" || game.IsFree
                                  || game.Price?.Equals("Free", StringComparison.OrdinalIgnoreCase) == true;

                // Skús načítať release dátum — zavolaj appdetails cez backend
                var releaseInfo = await GetWishlistReleaseInfoAsync(appId);

                return new WishlistGameModel
                {
                    AppId = appId,
                    Name = game.Name,
                    HeaderImageUrl = game.HeaderImageUrl,
                    Tags = game.Genres,
                    DateAddedUnix = dateAdded,
                    Price = game.Price,
                    OriginalPrice = game.OriginalPrice,
                    DiscountPercent = game.DiscountPercent,
                    IsFree = game.IsFree,
                    IsReleased = releaseInfo?.IsReleased ?? isReleased,
                    ReleaseDateDisplay = releaseInfo?.ReleaseDateDisplay ?? string.Empty,
                    ReleaseDateUnix = releaseInfo?.ReleaseDateUnix ?? 0,
                    IsEarlyAccess = releaseInfo?.IsEarlyAccess ?? false,
                    ReviewDesc = releaseInfo?.ReviewDesc ?? string.Empty,
                    ReviewCss = releaseInfo?.ReviewCss ?? string.Empty,
                    PlatformWindows = releaseInfo?.PlatformWindows ?? true,
                    PlatformMac = releaseInfo?.PlatformMac ?? false
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[API] GetWishlistGameDetail error {appId}: {ex.Message}");
                return null;
            }
        }

        private static async Task<WishlistGameModel?> GetWishlistReleaseInfoAsync(int appId)
        {
            try
            {
                var response = await _wishlistHttp.GetStringAsync(
                    $"{BaseUrl}/api/steam/wishlist/detail/{appId}");
                return FromJson<WishlistGameModel>(response);
            }
            catch { return null; }
        }

        // ─── Response type (pridaj nižšie do sekcie RESPONSE TYPES) ──────────────────

        public class WishlistGamesResponse
        {
            public List<WishlistGameModel> Games { get; set; } = new();
        }

        public static async Task<GameModel?> GetGameDetailsAsync(int appId)
        {
            try
            {
                var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/game/{appId}");
                return FromJson<GameModel>(response);
            }
            catch { return null; }
        }

        // ─── FEATURED — dedikovaný Steam endpoint ─────────────────────────────
        // Uses Steam's featured categories — returns properly curated games

        public static async Task<List<GameModel>> GetFeaturedAsync()
        {
            try
            {
                var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/store/featured");
                return FromJson<GamesResponse>(response)?.Games ?? new List<GameModel>();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<List<GameModel>> GetNewTrendingAsync(int page = 0)
        {
            try
            {
                var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/store/newtrending?page={page}");
                return FromJson<GamesResponse>(response)?.Games ?? new List<GameModel>();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<List<GameModel>> GetTopSellersAsync(int page = 0)
        {
            try
            {
                var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/store/topsellers?page={page}");
                return FromJson<GamesResponse>(response)?.Games ?? new List<GameModel>();
            }
            catch { return new List<GameModel>(); }
        }

        public static async Task<List<GameModel>> GetSpecialsAsync(int page = 0)
        {
            try
            {
                var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/store/specials?page={page}");
                return FromJson<GamesResponse>(response)?.Games ?? new List<GameModel>();
            }
            catch { return new List<GameModel>(); }
        }
    }

    // ─── RESPONSE TYPES ───────────────────────────────────────────────────────

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
}