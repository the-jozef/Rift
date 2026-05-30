using Rift_App.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Rift_App.Languages;

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
        public string AvatarUrl { get; set; } = SessionManager.AvatarUrl;

        public string LastLoginDisplay
        {
            get
            {
                var diff = DateTime.UtcNow - LastLoginAt;
                if (diff.TotalMinutes < 1) return L.Get("time_just_now");
                if (diff.TotalHours < 1) return string.Format(L.Get("time_min_ago"), (int)diff.TotalMinutes);
                if (diff.TotalDays < 1) return L.Get("date_today");
                if (diff.TotalDays < 2) return L.Get("date_yesterday");
                if (diff.TotalDays < 7) return string.Format(L.Get("time_days_ago"), (int)diff.TotalDays);
                return LastLoginAt.ToString("dd.MM.yyyy", LanguageService.Current);
            }
        }
    }
}