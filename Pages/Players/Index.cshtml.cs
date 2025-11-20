using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NBADATA.Data;
using NBADATA.Models;
using NBADATA.Services;

namespace NBADATA.Pages.Players;

public class IndexModel : PageModel
{
    private readonly NBADbContext _db;
    private readonly NBAApiService _nbaApi;
    
    public IndexModel(NBADbContext db, NBAApiService nbaApi)
    {
        _db = db;
        _nbaApi = nbaApi;
    }

    public List<Player> Players { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? q { get; set; }
    [BindProperty(SupportsGet = true)] public string? error { get; set; }
    public bool ShowingApiResults { get; set; } = false;
    public string? ApiErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            ShowingApiResults = true;
            
            // Si hay una búsqueda específica
            if (!string.IsNullOrWhiteSpace(q))
            {
                var searchTerm = q.Trim();
                var apiPlayers = await _nbaApi.SearchPlayersAsync(searchTerm);
                
                if (apiPlayers.Any())
                {
                    // Convertir los resultados de la API al modelo Player
                    // Obtener estadísticas para los jugadores encontrados
                    Players = new List<Player>();
                    foreach (var apiPlayer in apiPlayers.Take(50)) // Limitar a 50 para no sobrecargar
                    {
                        var player = await ConvertToPlayerWithStatsAsync(apiPlayer);
                        Players.Add(player);
                    }
                }
                else
                {
                    ApiErrorMessage = $"No se encontraron jugadores con el criterio '{searchTerm}'. Intenta con otro nombre.";
                    // Fallback a base de datos local si existe
                    Players = await _db.Players
                        .Where(p => p.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.FullName)
                        .ToListAsync();
                    if (Players.Any())
                    {
                        ShowingApiResults = false;
                        ApiErrorMessage = "No se encontraron resultados en la API. Mostrando jugadores de la base de datos local.";
                    }
                }
            }
            else
            {
                // Sin búsqueda: obtener primera página de jugadores de la API (25 jugadores)
                var apiPlayers = await _nbaApi.GetAllPlayersAsync(perPage: 25, maxPages: 1);
                
                if (apiPlayers.Any())
                {
                    // Convertir sin estadísticas para carga más rápida (se pueden cargar después)
                    Players = apiPlayers.Select(ap => ConvertToPlayer(ap)).ToList();
                }
                else
                {
                    ApiErrorMessage = "No se pudieron cargar jugadores de la API.";
                    // Fallback a base de datos local
                    Players = await _db.Players.OrderBy(p => p.FullName).Take(25).ToListAsync();
                    if (Players.Any())
                    {
                        ShowingApiResults = false;
                        ApiErrorMessage = "Error conectando con la API. Mostrando jugadores de la base de datos local.";
                    }
                }
            }
        }
        catch
        {
            // Si falla la API, mostrar jugadores locales como fallback
            ShowingApiResults = false;
            ApiErrorMessage = "Error conectando con la API de NBA. Mostrando jugadores locales.";
            Players = await _db.Players.OrderBy(p => p.FullName).ToListAsync();
        }
    }

    /// <summary>
    /// Convierte un resultado de la API a Player con estadísticas
    /// </summary>
    private async Task<Player> ConvertToPlayerWithStatsAsync(PlayerSearchResult ap)
    {
        var player = ConvertToPlayer(ap);
        
        // Intentar obtener estadísticas de la temporada más reciente
        try
        {
            var currentYear = DateTime.Now.Year;
            var seasons = new[] { currentYear, currentYear - 1, currentYear - 2 };
            
            foreach (var season in seasons)
            {
                var stats = await _nbaApi.GetPlayerStatsAsync(ap.Id, season);
                if (stats != null && stats.GamesPlayed > 0)
                {
                    player.Pts = stats.Pts;
                    player.Reb = stats.Reb;
                    player.Ast = stats.Ast;
                    player.Stl = stats.Stl;
                    player.Blk = stats.Blk;
                    player.Tov = stats.Turnover;
                    player.FgPct = stats.FgPct;
                    player.TpPct = stats.Fg3Pct;
                    player.FtPct = stats.FtPct;
                    break; // Usar la primera temporada con datos
                }
            }
        }
        catch
        {
            // Si falla obtener estadísticas, dejar en 0
        }
        
        return player;
    }

    private Player ConvertToPlayer(PlayerSearchResult ap)
    {
        return new Player
        {
            Id = ap.Id,
            FullName = ap.FullName,
            Team = ap.Team.Abbreviation,
            Position = ap.Position,
            HeightCm = ParseHeight(ap.Height) ?? 0,
            WeightKg = ParseWeight(ap.Weight) ?? 0,
            BirthDate = DateTime.MinValue,
            Pts = 0,
            Reb = 0,
            Ast = 0,
            Stl = 0,
            Blk = 0,
            Tov = 0,
            FgPct = 0,
            TpPct = 0,
            FtPct = 0
        };
    }

    private int? ParseHeight(string? heightStr)
    {
        if (string.IsNullOrWhiteSpace(heightStr)) return null;
        
        // Formato típico: "6-7" (6 pies, 7 pulgadas)
        var parts = heightStr.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0], out var feet) && int.TryParse(parts[1], out var inches))
        {
            return (int)((feet * 30.48) + (inches * 2.54)); // Convertir a cm
        }
        
        return null;
    }

    private int? ParseWeight(string? weightStr)
    {
        if (string.IsNullOrWhiteSpace(weightStr)) return null;
        
        // Formato típico: "215" (libras)
        if (int.TryParse(weightStr, out var pounds))
        {
            return (int)(pounds * 0.453592); // Convertir a kg
        }
        
        return null;
    }

    public IActionResult OnPostCompare([FromForm] int[] selected, [FromForm] string mode)
    {
        if (selected.Length != 2)
            return RedirectToPage(new { q, error = "Selecciona exactamente 2 jugadores" });

        return RedirectToPage("/Compare", new { ids = string.Join(",", selected), mode });
    }
}
