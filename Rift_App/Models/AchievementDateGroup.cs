using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Models
{
    /// <summary>
    /// Groups achievements by their unlock date.
    /// Used in the Library game detail activity feed.
    /// </summary>
    public class AchievementDateGroup
    {
        public string DateLabel { get; set; } = string.Empty;
        public List<AchievementModel> Items { get; set; } = new();
    }
}