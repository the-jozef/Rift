using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Models
{
    public class GameModel
    {
        public int AppId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HeaderImageUrl { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public int DiscountPercent { get; set; } = 0;
        public bool HasDiscount => DiscountPercent > 0;
        public List<string> Genres { get; set; } = new();
        public string GenresDisplay => string.Join(", ", Genres);
        public List<string> Screenshots { get; set; } = new();
        public string SteamStoreUrl { get; set; } = string.Empty;
        public int PlaytimeMinutes { get; set; } = 0;
        public string PlaytimeDisplay =>
            PlaytimeMinutes == 0 ? "Never played" :
            PlaytimeMinutes < 60 ? $"{PlaytimeMinutes} mins" :
            $"{(PlaytimeMinutes / 60.0):F1} hrs";
    }
}