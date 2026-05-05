using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rift_App.ViewModels;

namespace Rift_App.Models
{
    public class GameModel
    {
        public int AppId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HeaderImageUrl { get; set; } = string.Empty;

        // Steam CDN URL — saved in cache JSON
        public string IconUrl { get; set; } = string.Empty;

        // Local disk path — reconstructed at runtime, not saved to JSON
        [Newtonsoft.Json.JsonIgnore]
        public string? IconPath { get; set; }

        public string Description { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string OriginalPrice { get; set; } = string.Empty;
        public int DiscountPercent { get; set; } = 0;

        public bool HasDiscount => DiscountPercent > 0;
        public bool IsFree =>
            Price?.Equals("Free", StringComparison.OrdinalIgnoreCase) == true ||
            Price?.Equals("Free To Play", StringComparison.OrdinalIgnoreCase) == true;

        public List<string> Genres { get; set; } = new();
        public string GenresDisplay => string.Join(", ", Genres);
        public List<string> FeaturedTags => Genres.Take(5).ToList();

        public List<string> Screenshots { get; set; } = new();
        public string SteamStoreUrl { get; set; } = string.Empty;

        public int PlaytimeMinutes { get; set; } = 0;
        public string PlaytimeDisplay =>
            PlaytimeMinutes == 0 ? "Never played" :
            PlaytimeMinutes < 60 ? $"{PlaytimeMinutes} mins" :
            $"{(PlaytimeMinutes / 60.0):F1} hrs";

        public string StatusText { get; set; } = "Available Now";
        public bool IsRecommended => StatusText == "Recommended";

        [Newtonsoft.Json.JsonIgnore]
        private GameImageViewModel? _iconImage;

        [Newtonsoft.Json.JsonIgnore]
        public GameImageViewModel IconImage =>
            _iconImage ??= new GameImageViewModel(IconUrl);

        [Newtonsoft.Json.JsonIgnore]
        private GameImageViewModel? _headerImage;

        [Newtonsoft.Json.JsonIgnore]
        public GameImageViewModel HeaderImage =>
            _headerImage ??= new GameImageViewModel(HeaderImageUrl);
    }
}