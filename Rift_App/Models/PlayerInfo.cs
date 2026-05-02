using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Models
{
    public class PlayerInfo
    {
        public string SteamId { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string AvatarUrl { get; set; } = string.Empty;

        public string ProfileUrl { get; set; } = string.Empty;
    }
}