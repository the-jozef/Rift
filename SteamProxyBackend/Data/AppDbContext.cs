using Microsoft.EntityFrameworkCore;
using SteamProxyBackend.Models;
using System.Reflection.Emit;

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
            modelBuilder.Entity<User>().ToTable("Users")
                .HasIndex(u => u.Username).IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.SteamId64).IsUnique();

            modelBuilder.Entity<Device>().ToTable("Devices")
                .HasIndex(d => d.DeviceToken).IsUnique();

            // ← PRIDANÉ:
            modelBuilder.Entity<Device>()
                .HasMany(d => d.DeviceAccounts)
                .WithOne()
                .HasForeignKey(da => da.DeviceToken)
                .HasPrincipalKey(d => d.DeviceToken);

            modelBuilder.Entity<Device>()
                .HasMany(d => d.LoginHistories)
                .WithOne()
                .HasForeignKey(lh => lh.DeviceToken)
                .HasPrincipalKey(d => d.DeviceToken);

            modelBuilder.Entity<DeviceAccount>().ToTable("DeviceAccounts")
                .HasIndex(da => new { da.DeviceToken, da.UserId }).IsUnique();

            modelBuilder.Entity<LoginHistory>().ToTable("LoginHistories");
        }
    }
}