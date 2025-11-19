using System.Reflection;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NBADATA.Models;
using NBADATA.Data;
using NBADATA.Services;

namespace NBADATA.Pages
{
    public class CompareModel : PageModel
    {
        private readonly NBADbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly NBAApiService _nbaApi;

        public CompareModel(NBADbContext db, IWebHostEnvironment env, NBAApiService nbaApi) 
        {
            _db = db;
            _env = env;
            _nbaApi = nbaApi;
        }

        [BindProperty(SupportsGet = true)]
        public string? Mode { get; set; } = "basic";

        [BindProperty(SupportsGet = true, Name = "ids")]
        public string? IdsRaw { get; set; }

        [BindProperty(SupportsGet = true, Name = "player1")]
        public string? Player1 { get; set; }

        [BindProperty(SupportsGet = true, Name = "player2")]
        public string? Player2 { get; set; }

        public int[] SelectedIds { get; private set; } = Array.Empty<int>();
        public List<Player> Players { get; private set; } = new();
        public Player? Result1 { get; private set; }
        public Player? Result2 { get; private set; }
        public List<PropertyInfo> Props { get; private set; } = new();
        public string DebugMsg { get; private set; } = "";
        public string? PhotoUrl1 { get; private set; }
        public string? PhotoUrl2 { get; private set; }

        public async Task OnGet()
        {
            SelectedIds = ParseIds(IdsRaw);
            DebugMsg = $"IdsRaw='{IdsRaw}' -> {SelectedIds.Length} ids: [{string.Join(",", SelectedIds)}]";

            if (SelectedIds.Length > 0)
            {
                Players = await _db.Set<Player>()
                                   .Where(p => SelectedIds.Contains(p.Id))
                                   .ToListAsync();
                DebugMsg += $" | Players found: {Players.Count}";
            }

            if (!string.IsNullOrWhiteSpace(Player1))
            {
                var term = Player1.Trim();
                
                // Buscar en la API
                try
                {
                    // Estrategia: buscar por el apellido (última palabra) que es más específico
                    var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var searchTerm = words.Length > 1 ? words.Last() : term; // Usar apellido si hay múltiples palabras
                    
                    var apiResults = await _nbaApi.SearchPlayersAsync(searchTerm);
                    
                    DebugMsg += $" | Búsqueda '{searchTerm}' devolvió {apiResults.Count} resultados";
                    
                    // 1. Primero intentar coincidencia exacta del nombre completo
                    var apiPlayer = apiResults.FirstOrDefault(p => 
                        p.FullName.Equals(term, StringComparison.OrdinalIgnoreCase));
                    
                    // 2. Si no hay coincidencia exacta, buscar donde TODAS las palabras originales aparezcan
                    if (apiPlayer == null && words.Length > 1)
                    {
                        apiPlayer = apiResults.FirstOrDefault(p =>
                        {
                            var fullNameLower = p.FullName.ToLower();
                            return words.All(word => fullNameLower.Contains(word.ToLower()));
                        });
                    }
                    
                    // 3. Si aún no encontramos, buscar por coincidencia parcial con el término original
                    if (apiPlayer == null)
                    {
                        apiPlayer = apiResults.FirstOrDefault(p => 
                            p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (apiPlayer != null)
                    {
                        DebugMsg += $" | ✓ Encontrado: {apiPlayer.FullName}";
                        Result1 = await ConvertToPlayerWithStatsAsync(apiPlayer);
                    }
                    else
                    {
                        DebugMsg += " | ✗ No encontrado";
                    }
                }
                catch (Exception ex)
                {
                    DebugMsg += $" | Error API: {ex.Message}";
                    // Si falla la API, buscar en DB local
                    Result1 = await _db.Players
                                       .Where(p => EF.Functions.Like(p.FullName, $"%{term}%"))
                                       .FirstOrDefaultAsync();

                    if (Result1 == null)
                    {
                        var all = await _db.Players.ToListAsync();
                        Result1 = all.FirstOrDefault(p =>
                            p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (Result1 != null && !Players.Any(p => p.Id == Result1.Id))
                    Players.Add(Result1);

                DebugMsg += $" | Player1='{term}' -> {(Result1 != null ? Result1.FullName : "(not found)")}";
            }

            if (!string.IsNullOrWhiteSpace(Player2))
            {
                var term = Player2.Trim();
                
                // Buscar en la API
                try
                {
                    // Estrategia: buscar por el apellido (última palabra) que es más específico
                    var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var searchTerm = words.Length > 1 ? words.Last() : term; // Usar apellido si hay múltiples palabras
                    
                    var apiResults = await _nbaApi.SearchPlayersAsync(searchTerm);
                    
                    // 1. Primero intentar coincidencia exacta del nombre completo
                    var apiPlayer = apiResults.FirstOrDefault(p => 
                        p.FullName.Equals(term, StringComparison.OrdinalIgnoreCase));
                    
                    // 2. Si no hay coincidencia exacta, buscar donde TODAS las palabras originales aparezcan
                    if (apiPlayer == null && words.Length > 1)
                    {
                        apiPlayer = apiResults.FirstOrDefault(p =>
                        {
                            var fullNameLower = p.FullName.ToLower();
                            return words.All(word => fullNameLower.Contains(word.ToLower()));
                        });
                    }
                    
                    // 3. Si aún no encontramos, buscar por coincidencia parcial con el término original
                    if (apiPlayer == null)
                    {
                        apiPlayer = apiResults.FirstOrDefault(p => 
                            p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (apiPlayer != null)
                    {
                        Result2 = await ConvertToPlayerWithStatsAsync(apiPlayer);
                    }
                }
                catch
                {
                    // Si falla la API, buscar en DB local
                    Result2 = await _db.Players
                                       .Where(p => EF.Functions.Like(p.FullName, $"%{term}%"))
                                       .FirstOrDefaultAsync();

                    if (Result2 == null)
                    {
                        var all = await _db.Players.ToListAsync();
                        Result2 = all.FirstOrDefault(p =>
                            p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (Result2 != null && !Players.Any(p => p.Id == Result2.Id))
                    Players.Add(Result2);

                DebugMsg += $" | Player2='{term}' -> {(Result2 != null ? Result2.FullName : "(not found)")}";
            }

            var t = typeof(Player);
            Props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p =>
                        IsSimple(p.PropertyType) &&
                        p.Name != "Id")
                     .OrderBy(p => p.Name)
                     .ToList();
            DeterminePhotoUrls();
        }

        private async Task<Player> ConvertToPlayerWithStatsAsync(PlayerSearchResult ap)
        {
            var player = new Player
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

            // Intentar obtener estadísticas de la API (probar múltiples temporadas)
            PlayerAverageStats? stats = null;
            try
            {
                // Intentar últimas 3 temporadas
                var seasons = new[] { 2024, 2023, 2022 };
                
                foreach (var season in seasons)
                {
                    stats = await _nbaApi.GetPlayerStatsAsync(ap.Id, season);
                    if (stats != null && stats.GamesPlayed > 0)
                    {
                        DebugMsg += $" | Stats de temporada {season} ({stats.GamesPlayed} juegos)";
                        break;
                    }
                }
                
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
                }
                else
                {
                    DebugMsg += $" | {ap.FullName}: Sin stats en 2024/2023/2022";
                }
            }
            catch (Exception ex)
            {
                DebugMsg += $" | Error stats {ap.FullName}: {ex.Message}";
            }

            return player;
        }

        private int? ParseHeight(string? heightStr)
        {
            if (string.IsNullOrWhiteSpace(heightStr)) return null;
            
            var parts = heightStr.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var feet) && int.TryParse(parts[1], out var inches))
            {
                return (int)((feet * 30.48) + (inches * 2.54));
            }
            
            return null;
        }

        private int? ParseWeight(string? weightStr)
        {
            if (string.IsNullOrWhiteSpace(weightStr)) return null;
            
            if (int.TryParse(weightStr, out var pounds))
            {
                return (int)(pounds * 0.453592);
            }
            
            return null;
        }

        private void DeterminePhotoUrls()
        {
            PhotoUrl1 = FindPhotoUrl(Result1);
            PhotoUrl2 = FindPhotoUrl(Result2);
        }

        private string? FindPhotoUrl(Player? p)
        {
            if (p == null) return null;
            
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var imagesDir = Path.Combine(webRoot, "images");
            
            string Normalize(string name)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var ch in name)
                {
                    if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                }
                return sb.ToString();
            }
            
            var candidates = new List<string>();
            candidates.Add($"{p.Id}.jpg");
            candidates.Add($"{p.Id}.png");
            
            var nameNoSpaces = Normalize(p.FullName);
            candidates.Add(nameNoSpaces + ".png");
            candidates.Add(nameNoSpaces + ".jpg");
            candidates.Add(nameNoSpaces.ToLowerInvariant() + ".png");
            candidates.Add(nameNoSpaces.ToLowerInvariant() + ".jpg");

            foreach (var file in candidates)
            {
                var path = Path.Combine(imagesDir, file);
                if (System.IO.File.Exists(path))
                {
                    return "/images/" + file;
                }
            }

            return null;
        }

        private static int[] ParseIds(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<int>();

            return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim())
                      .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                      .Where(n => n.HasValue)
                      .Select(n => n!.Value)
                      .Distinct()
                      .ToArray();
        }

        private static bool IsSimple(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t.IsPrimitive
                || t.IsEnum
                || t == typeof(string)
                || t == typeof(decimal)
                || t == typeof(double)
                || t == typeof(float)
                || t == typeof(DateTime)
                || t == typeof(Guid);
        }
    }
}
