using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NBADATA.Data;
using NBADATA.Models;

namespace NBADATA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlayerController : ControllerBase
    {
        private readonly NBADbContext _context;

        public PlayerController(NBADbContext context)
        {
            _context = context;
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<PlayerSuggestion>>> SearchPlayers([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Ok(new List<PlayerSuggestion>());
            }

            var players = await _context.Players
                .Where(p => p.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.FullName)
                .Take(10)
                .Select(p => new PlayerSuggestion
                {
                    Id = p.Id,
                    FullName = p.FullName,
                    Team = p.Team,
                    Position = p.Position
                })
                .ToListAsync();

            return Ok(players);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Player>> GetPlayer(int id)
        {
            var player = await _context.Players.FindAsync(id);
            
            if (player == null)
            {
                return NotFound();
            }

            return player;
        }
    }

    public class PlayerSuggestion
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Team { get; set; } = "";
        public string Position { get; set; } = "";
    }
}
