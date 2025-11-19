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

        [BindProperty(SupportsGet = true)]
        public int Season { get; set; } = 2024; // Temporada por defecto

        public int[] SelectedIds { get; private set; } = Array.Empty<int>();
        public List<Player> Players { get; private set; } = new();
        public Player? Result1 { get; private set; }
        public Player? Result2 { get; private set; }
        public List<PropertyInfo> Props { get; private set; } = new();
        public List<int> AvailableSeasons { get; private set; } = new();
        public string? PhotoUrl1 { get; private set; }
        public string? PhotoUrl2 { get; private set; }

        public async Task OnGet()
        {
            // Obtener temporadas disponibles
            AvailableSeasons = await _nbaApi.GetAvailableSeasonsAsync();
            if (!AvailableSeasons.Any())
            {
                // Fallback si no se pueden obtener temporadas
                var currentYear = DateTime.Now.Year;
                AvailableSeasons = Enumerable.Range(2015, currentYear - 2014).Reverse().ToList();
            }

            // Validar que la temporada seleccionada sea válida
            if (!AvailableSeasons.Contains(Season))
            {
                Season = AvailableSeasons.FirstOrDefault(); // Usar la primera disponible
            }

            SelectedIds = ParseIds(IdsRaw);

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
                        Result1 = await ConvertToPlayerWithStatsAsync(apiPlayer, Season);
                    }
                }
                catch
                {
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
                        Result2 = await ConvertToPlayerWithStatsAsync(apiPlayer, Season);
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
            }

            // Filtrar propiedades para la comparación (excluir BirthDate y otras no relevantes)
            var t = typeof(Player);
            Props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p =>
                        IsSimple(p.PropertyType) &&
                        p.Name != "Id" &&
                        p.Name != "BirthDate") // Excluir fecha de nacimiento
                     .OrderBy(p => GetPropertyOrder(p.Name))
                     .ToList();
            DeterminePhotoUrls();
        }

        /// <summary>
        /// Ordena las propiedades para mostrar primero las estadísticas relevantes
        /// </summary>
        private int GetPropertyOrder(string propertyName)
        {
            return propertyName switch
            {
                "FullName" => 1,
                "Team" => 2,
                "Position" => 3,
                "Pts" => 4,
                "Reb" => 5,
                "Ast" => 6,
                "Stl" => 7,
                "Blk" => 8,
                "Tov" => 9,
                "FgPct" => 10,
                "TpPct" => 11,
                "FtPct" => 12,
                "HeightCm" => 13,
                "WeightKg" => 14,
                _ => 99
            };
        }

        /// <summary>
        /// Convierte un resultado de la API a Player con estadísticas de la temporada especificada
        /// </summary>
        private async Task<Player> ConvertToPlayerWithStatsAsync(PlayerSearchResult ap, int season)
        {
            var player = new Player
            {
                Id = ap.Id,
                FullName = ap.FullName,
                Team = ap.Team.Abbreviation,
                Position = ap.Position,
                HeightCm = ParseHeight(ap.Height) ?? 0,
                WeightKg = ParseWeight(ap.Weight) ?? 0,
                BirthDate = DateTime.MinValue, // No se muestra en la comparación
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

            // Obtener estadísticas de la temporada especificada
            try
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
                }
            }
            catch
            {
                // Si falla obtener estadísticas, dejar en 0
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
