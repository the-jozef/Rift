using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rift_App.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public string OriginalPrice { get; set; } = string.Empty;
        public int DiscountPercent { get; set; } = 0;
        public string InstallDir { get; set; } = string.Empty;

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
        public string PlaytimeDisplay
        {
            get
            {
                if (PlaytimeMinutes <= 0)
                    return "Never played";
    
                if (PlaytimeMinutes < 60)
                    return $"{PlaytimeMinutes} mins";
        
                double hours = PlaytimeMinutes / 60.0;
           
                return hours % 1 == 0
                    ? $"{(int)hours} hrs"
                    : $"{hours:F1} hrs";
            }
        }

        // Set at runtime by SteamInstallService — not saved in JSON
        [Newtonsoft.Json.JsonIgnore]
        public bool IsInstalled { get; set; }

        // Local disk path for icon — set at runtime by LibraryCacheService
        [Newtonsoft.Json.JsonIgnore]
        public string? IconPath { get; set; }

        public string StatusText { get; set; } = "Available Now";
        public bool IsRecommended { get; set; } = false;

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