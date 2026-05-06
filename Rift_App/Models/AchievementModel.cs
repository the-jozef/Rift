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

        // Set at runtime by LibraryGameViewModel.BuildPreviews()
        // Used in XAML to show Name + Description only for the first item in the unlocked row
        [Newtonsoft.Json.JsonIgnore]
        public bool IsFirst { get; set; }

        public string UnlockTimeDisplay =>
            UnlockTime.HasValue ? UnlockTime.Value.ToString("MMM d, yyyy") : string.Empty;

        // Shows the colored icon if unlocked, gray icon if locked
        [Newtonsoft.Json.JsonIgnore]
        private GameImageViewModel? _iconImage;

        [Newtonsoft.Json.JsonIgnore]
        public GameImageViewModel IconImage =>
            _iconImage ??= new GameImageViewModel(Unlocked ? IconUrl : IconGrayUrl);
    }
}