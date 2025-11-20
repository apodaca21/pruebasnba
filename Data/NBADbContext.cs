using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NBADATA.Models;

namespace NBADATA.Data
{
    public class NBADbContext : IdentityDbContext<ApplicationUser>
    {
        public NBADbContext(DbContextOptions<NBADbContext> options) : base(options) { }
        public DbSet<Player> Players { get; set; } = null!;
        public DbSet<FavoritePlayer> FavoritePlayers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Un mismo usuario no puede tener el mismo jugador 2 veces en favoritos
            builder.Entity<FavoritePlayer>()
                .HasIndex(f => new { f.UserId, f.PlayerId })
                .IsUnique();
        }
    }
}
