using Microsoft.AspNetCore.Identity;

namespace NBADATA.Models
{
    public class ApplicationUser : IdentityUser
    {
        public ICollection<FavoritePlayer> FavoritePlayers { get; set; } = new List<FavoritePlayer>();
    }
}
