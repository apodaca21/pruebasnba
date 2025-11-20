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

    // Lista final que va a la vista
    public List<Player> Players { get; set; } = new();

    // Parámetros GET
    [BindProperty(SupportsGet = true)] public string? q { get; set; }
    [BindProperty(SupportsGet = true)] public string? error { get; set; }

    // Mensajes
    public bool ShowingApiResults { get; set; } = false;
    public string? ApiErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            ShowingApiResults = true;

            // ==============================================
            // 1️⃣ CUANDO HAY BÚSQUEDA
            // ==============================================
            if (!string.IsNullOrWhiteSpace(q))
            {
                var searchTerm = q.Trim();

                // Buscar en API
                var apiPlayers = await _nbaApi.SearchPlayersAsync(searchTerm);

                if (apiPlayers.Any())
                {
                    Players = apiPlayers
                        .Take(50)
                        .Select(ap => ConvertToListPlayer(ap))
                        .ToList();
                }
                else
                {
                    ApiErrorMessage = $"No se encontraron jugadores con '{searchTerm}'. Mostrando jugadores locales.";

                    // Buscar en DB local
                    Players = await _db.Players
                        .Where(p => p.FullName.Contains(searchTerm))
                        .OrderBy(p => p.FullName)
                        .ToListAsync();

                    ShowingApiResults = false;
                }
            }
            else
            {
                // ==============================================
                // 2️⃣ CUANDO NO HAY BÚSQUEDA — PÁGINA PRINCIPAL
                // ==============================================

                // Cargar primera página desde API
                var apiPlayers = await _nbaApi.GetAllPlayersAsync(perPage: 25, maxPages: 1);

                if (apiPlayers.Any())
                {
                    Players = apiPlayers.Select(ap => ConvertToListPlayer(ap)).ToList();
                }
                else
                {
                    ApiErrorMessage = "Error conectando con API. Mostrando jugadores locales.";
                    Players = await _db.Players.OrderBy(p => p.FullName).Take(25).ToListAsync();
                    ShowingApiResults = false;
                }
            }
        }
        catch
        {
            // ==============================================
            // Error general → fallback a DB local
            // ==============================================
            ApiErrorMessage = "Error conectando con la API de NBA. Mostrando jugadores locales.";
            ShowingApiResults = false;

            Players = await _db.Players.OrderBy(p => p.FullName).ToListAsync();
        }
    }

    // ===============================================================================
    // CONVERSIÓN para LISTA de Players
    // SOLO nombre + equipo + posición (sin stats)
    // ===============================================================================

    private Player ConvertToListPlayer(PlayerSearchResult ap)
    {
        return new Player
        {
            Id = ap.Id,
            FullName = ap.FullName,
            Team = ap.Team.Abbreviation,
            Position = string.IsNullOrWhiteSpace(ap.Position) ? "-" : ap.Position,

            // Valores default porque NO USAS stats aquí
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

    // ===============================================================================
    // Conversores de altura y peso
    // ===============================================================================
    private int? ParseHeight(string? heightStr)
    {
        if (string.IsNullOrWhiteSpace(heightStr)) return null;

        var parts = heightStr.Split('-');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var feet) &&
            int.TryParse(parts[1], out var inches))
        {
            return (int)((feet * 30.48) + (inches * 2.54));
        }

        return null;
    }

    private int? ParseWeight(string? weightStr)
    {
        if (string.IsNullOrWhiteSpace(weightStr)) return null;

        if (int.TryParse(weightStr, out var pounds))
            return (int)(pounds * 0.453592);

        return null;
    }

    // ===============================================================================
    // POST → cuando se hace clic en "Comparar jugadores"
    // ===============================================================================
    public IActionResult OnPostCompare([FromForm] int[] selected, [FromForm] string mode)
    {
        if (selected.Length < 1)
            return RedirectToPage(new { q, error = "Selecciona al menos 1 jugador" });

        return RedirectToPage("/Compare/Index", new { selectedPlayers = selected });
    }
}
