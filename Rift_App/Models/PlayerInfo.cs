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
        [JsonProperty("steamId")]
        public string SteamId { get; set; } = string.Empty;

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; } = string.Empty;

        [JsonProperty("profileUrl")]
        public string ProfileUrl { get; set; } = string.Empty;
    }
}