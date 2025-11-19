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
                var apiPlayers = await _nbaApi.SearchPlayersAsync(q);
                
                if (apiPlayers.Any())
                {
                    // Convertir los resultados de la API al modelo Player
                    Players = apiPlayers.Select(ap => ConvertToPlayer(ap)).ToList();
                }
                else
                {
                    ApiErrorMessage = "No se encontraron jugadores con ese criterio.";
                }
            }
            else
            {
                // Sin búsqueda: obtener todos los jugadores de la API (primeras 2 páginas = ~200 jugadores para que cargue más rápido)
                var apiPlayers = await _nbaApi.GetAllPlayersAsync(100, 2);
                
                if (apiPlayers.Any())
                {
                    Players = apiPlayers.Select(ap => ConvertToPlayer(ap)).ToList();
                }
                else
                {
                    ApiErrorMessage = "No se pudieron cargar jugadores de la API.";
                }
            }
        }
        catch (Exception ex)
        {
            // Si falla la API, mostrar jugadores locales como fallback
            ShowingApiResults = false;
            ApiErrorMessage = "Error conectando con la API de NBA. Mostrando jugadores locales.";
            Players = await _db.Players.OrderBy(p => p.FullName).ToListAsync();
        }
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
