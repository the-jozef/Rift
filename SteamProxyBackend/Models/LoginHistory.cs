using SteamProxyBackend.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SteamProxyBackend.Models
{
    public class LoginHistory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public string DeviceToken { get; set; } = string.Empty;
        public DateTime LoginAt { get; set; } = DateTime.UtcNow;
        public string LoginMethod { get; set; } = string.Empty;
    }
}