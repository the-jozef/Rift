namespace SteamProxyBackend.DTOs
{
    // ─── AUTH REQUESTS ────────────────────────────────────────────────────────

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SteamId64 { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
    }

    public class SteamLoginRequest
    {
        public string SteamId64 { get; set; } = string.Empty;
        public string DeviceToken { get; set; } = string.Empty;
    }

    // ─── DEVICE REQUESTS ──────────────────────────────────────────────────────

    public class DeviceInitRequest
    {
        public string DeviceToken { get; set; } = string.Empty;
    }

    public class SessionRequest
    {
        public string DeviceToken { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string LastLocation { get; set; } = "Store";
    }

    // ─── RESPONSES ────────────────────────────────────────────────────────────

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string? Username { get; set; }
        public string? SteamId64 { get; set; }
    }

    public class AccountInfo
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string SteamId64 { get; set; } = string.Empty;
        public string LastLocation { get; set; } = "Store";
        public DateTime LastLoginAt { get; set; }
        public bool IsLastActive { get; set; }
    }

    public class SessionResponse
    {
        public bool HasSession { get; set; }
        public Guid? UserId { get; set; }
        public string? Username { get; set; }
        public string? SteamId64 { get; set; }
        public string LastLocation { get; set; } = "Store";
    }
}