using Rift_App.Languages;
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

        public string StatusDisplay => Status switch
        {
            "Online" => L.Get("status_online"),
            "In-Game" => L.Get("status_ingame"),
            "Away" => L.Get("status_away"),
            "Busy" => L.Get("status_busy"),
            "Snooze" => L.Get("status_snooze"),
            "Looking to Trade" => L.Get("status_trade"),
            "Looking to Play" => L.Get("status_play"),
            _ => L.Get("status_offline")
        };
    }
    public class FriendsResponse
    {
        public bool IsPrivate { get; set; }
        public List<FriendModel> Friends { get; set; } = new();
    }
}