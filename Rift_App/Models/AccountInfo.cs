using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Models
{
    public class AccountInfo
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string SteamId64 { get; set; } = string.Empty;
        public string LastLocation { get; set; } = "Store";
        public DateTime LastLoginAt { get; set; }
        public bool IsLastActive { get; set; }
        public string AvatarUrl { get; set; } = string.Empty;

        public string LastLoginDisplay
        {
            get
            {
                var diff = DateTime.UtcNow - LastLoginAt;
                if (diff.TotalMinutes < 1) return "Just now";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} min ago";
                if (diff.TotalDays < 1) return "Today";
                if (diff.TotalDays < 2) return "Yesterday";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
                return LastLoginAt.ToString("dd.MM.yyyy");
            }
        }
    }
}