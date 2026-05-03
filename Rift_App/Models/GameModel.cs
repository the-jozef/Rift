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

        // Aktuálna cena — current price (môže byť "Free", "$9.99", atď.)
        public string Price { get; set; } = string.Empty;

        // Originálna cena pred zľavou — original price before discount
        public string OriginalPrice { get; set; } = string.Empty;

        public int DiscountPercent { get; set; } = 0;
        public bool HasDiscount => DiscountPercent > 0;
        public bool IsFree => Price?.Equals("Free", StringComparison.OrdinalIgnoreCase) == true
                           || Price?.Equals("Free To Play", StringComparison.OrdinalIgnoreCase) == true;

        public List<string> Genres { get; set; } = new();
        public string GenresDisplay => string.Join(", ", Genres);

        // Max 5 tagov pre featured panel — max 5 tags for featured panel
        public List<string> FeaturedTags => Genres.Take(5).ToList();

        public List<string> Screenshots { get; set; } = new();
        public string SteamStoreUrl { get; set; } = string.Empty;

        public int PlaytimeMinutes { get; set; } = 0;
        public string PlaytimeDisplay =>
            PlaytimeMinutes == 0 ? "Never played" :
            PlaytimeMinutes < 60 ? $"{PlaytimeMinutes} mins" :
            $"{(PlaytimeMinutes / 60.0):F1} hrs";

        // Status pre featured sekciu — "Recommended" alebo "Available Now"
        public string StatusText { get; set; } = "Available Now";
        public bool IsRecommended => StatusText == "Recommended";
    }
}