using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using NBADATA.Data;
using NBADATA.Models;

namespace NBADATA.Pages.Profile
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly NBADbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(NBADbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public List<FavoritePlayer> Favorites { get; set; } = new();
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            UserEmail = user.Email;
            UserName = user.UserName;

            Favorites = await _db.FavoritePlayers
                .Where(f => f.UserId == user.Id)
                .OrderBy(f => f.PlayerName)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostRemoveFavoriteAsync(int favoriteId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login");

            var favorite = await _db.FavoritePlayers
                .FirstOrDefaultAsync(f => f.Id == favoriteId && f.UserId == user.Id);

            if (favorite != null)
            {
                _db.FavoritePlayers.Remove(favorite);
                await _db.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
