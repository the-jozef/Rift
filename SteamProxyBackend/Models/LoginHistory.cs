using SteamProxyBackend.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SteamProxyBackend.Models
{
    public class LoginHistory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public string DeviceToken { get; set; } = string.Empty;

        public DateTime LoginAt { get; set; } = DateTime.UtcNow;

        // "Steam" or "Rift"
        [Required]
        public string LoginMethod { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}