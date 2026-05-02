using Newtonsoft.Json;
using Rift_App.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    public static class ApiService
    {
        // RENDER URL 
        private const string BaseUrl = "https://rift-hupv.onrender.com";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static StringContent ToJson(object obj) =>
            new StringContent(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json");

        private static T? FromJson<T>(string json) => JsonConvert.DeserializeObject<T>(json);

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
            for (int i = 0; i < 3; i++) // max 3 pokusy
            {
                try
                {
                    var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/player/{steamId64}");
                    return FromJson<PlayerInfo>(response);
                }
                catch { await Task.Delay(2000); } // počkaj 2s a skús znova
            }
            return null;
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

        public static async Task<List<GameModel>> GetWishlistAsync(string steamId64)
        {
            try
            {
                var response = await _http.GetStringAsync($"{BaseUrl}/api/steam/wishlist/{steamId64}");
                return FromJson<GamesResponse>(response)?.Games ?? new List<GameModel>();
            }
            catch { return new List<GameModel>(); }
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

    // ─── RESPONSE TYPES — only for API communication ──────────────────────────
    // GameModel, PlayerInfo, AccountInfo sú v Rift_App.Models — nie tu

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

    // GamesResponse uses GameModel from Rift_App.Models via using directive
    public class GamesResponse
    {
        public List<GameModel> Games { get; set; } = new();
    }
}