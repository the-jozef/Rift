using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rift_App.ViewModels;

namespace Rift_App.Models
{
    public class AchievementModel
    {
        public string ApiName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public string IconGrayUrl { get; set; } = string.Empty;
        public bool Unlocked { get; set; }
        public DateTime? UnlockTime { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public bool IsFirst { get; set; }

        public string UnlockTimeDisplay =>
            UnlockTime.HasValue ? UnlockTime.Value.ToString("MMM d, yyyy") : string.Empty;

        [Newtonsoft.Json.JsonIgnore]
        public string? LocalIconPath { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public string? LocalIconGrayPath { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        private GameImageViewModel? _iconImage;

        [Newtonsoft.Json.JsonIgnore]
        public GameImageViewModel IconImage
        {
            get
            {
                if (_iconImage != null) return _iconImage;

                // Use local path if exists, otherwise fall back to URL
                var source = Unlocked
                    ? (!string.IsNullOrEmpty(LocalIconPath) ? LocalIconPath : IconUrl)
                    : (!string.IsNullOrEmpty(LocalIconGrayPath) ? LocalIconGrayPath : IconGrayUrl);

                _iconImage = new GameImageViewModel(source ?? string.Empty);
                return _iconImage;
            }
        }

        // Call this to reset icon so it reloads with new paths
      
        public void ResetIconImage() => _iconImage = null;
    }
}