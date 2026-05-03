using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Services
{
    public static class SessionManager
    {
        public static string SteamId64 { get; private set; } = string.Empty;
        public static Guid UserId { get; private set; } = Guid.Empty;
        public static string Username { get; private set; } = string.Empty;
        public static string LastLocation { get; private set; } = "Store";

        // Avatar URL zo Steam API — Steam API avatar URL
        public static string AvatarUrl { get; private set; } = string.Empty;

        public static bool IsLoggedIn => UserId != Guid.Empty && !string.IsNullOrEmpty(SteamId64);

        public static void SetSession(Guid userId, string username, string steamId64, string lastLocation = "Store")
        {
            UserId = userId;
            Username = username;
            SteamId64 = steamId64;
            LastLocation = lastLocation;
        }

        // Nastaví avatar po načítaní Steam profilu
        // Sets avatar after Steam profile is loaded
        public static void SetAvatar(string avatarUrl)
        {
            AvatarUrl = avatarUrl;
        }

        public static void UpdateLocation(string location) => LastLocation = location;

        public static void Clear()
        {
            UserId = Guid.Empty;
            Username = string.Empty;
            SteamId64 = string.Empty;
            LastLocation = "Store";
            AvatarUrl = string.Empty;
        }
    }
}