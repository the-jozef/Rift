using Microsoft.EntityFrameworkCore;
using SteamProxyBackend.Models;

namespace SteamProxyBackend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Device> Devices { get; set; }
        public DbSet<DeviceAccount> DeviceAccounts { get; set; }
        public DbSet<LoginHistory> LoginHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username).IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.SteamId64).IsUnique();

            modelBuilder.Entity<Device>()
                .HasIndex(d => d.DeviceToken).IsUnique();

            modelBuilder.Entity<DeviceAccount>()
                .HasIndex(da => new { da.DeviceToken, da.UserId }).IsUnique();
        }
    }
}