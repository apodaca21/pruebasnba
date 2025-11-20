using NBADATA.Models;

namespace NBADATA.Models
{
    public class FavoritePlayer
    {
        public int Id { get; set; }              // PK

        public string UserId { get; set; } = ""; // FK a ApplicationUser
        public int PlayerId { get; set; }        // Id del jugador (balldontlie / Player.Id)
        public string PlayerName { get; set; } = "";
        public string Team { get; set; } = "";
        public string Position { get; set; } = "";

        public ApplicationUser User { get; set; } = null!;
    }
}
