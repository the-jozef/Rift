using System;
using System.Collections.Generic;
using System.Text;

namespace Rift_App.Models
{
    public class FriendModel
    {
        public string SteamId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string Status { get; set; } = "Offline";   // friend status offline, online, busy, away, snooze, looking to trade, looking to play
        public string CurrentGame { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
    }
    public class FriendsResponse
    {
        public bool IsPrivate { get; set; }
        public List<FriendModel> Friends { get; set; } = new();
    }
}