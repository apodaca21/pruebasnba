using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NBADATA.Models;

namespace NBADATA.Data
{
    public class NBADbContext : IdentityDbContext<ApplicationUser>
    {
        public NBADbContext(DbContextOptions<NBADbContext> options) : base(options) { }

        public DbSet<Player> Players { get; set; } = null!;
    }
}
