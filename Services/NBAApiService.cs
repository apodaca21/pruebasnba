using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace NBADATA.Services
{
    public class NBAApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NBAApiService> _logger;
        private readonly IMemoryCache _cache;

        public NBAApiService(HttpClient httpClient, ILogger<NBAApiService> logger, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
        }

        public async Task<List<PlayerSearchResult>> SearchPlayersAsync(string searchTerm)
        {
            // Optimización: Usar caché para búsquedas recientes (5 minutos)
            var cacheKey = $"search_players_{searchTerm.ToLowerInvariant()}";
            if (_cache.TryGetValue(cacheKey, out List<PlayerSearchResult>? cachedResults))
            {
                _logger.LogInformation("Resultados de búsqueda obtenidos del caché para: {SearchTerm}", searchTerm);
                return cachedResults ?? new List<PlayerSearchResult>();
            }

            try
            {
                var allPlayers = new List<PlayerSearchResult>();
                int? cursor = null;
                int maxPages = 2; // Reducido a 2 páginas (50 jugadores) para mejorar velocidad
                int currentPage = 0;

                do
                {
                    var url = cursor.HasValue 
                        ? $"players?search={Uri.EscapeDataString(searchTerm)}&cursor={cursor}&per_page=25"
                        : $"players?search={Uri.EscapeDataString(searchTerm)}&per_page=25";
                    
                    var response = await _httpClient.GetAsync(url);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Error en API: {StatusCode} para búsqueda: {SearchTerm}", response.StatusCode, searchTerm);
                        break;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<PlayersResponseWithMeta>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Data == null || !result.Data.Any())
                        break;

                    allPlayers.AddRange(result.Data);
                    cursor = result.Meta?.NextCursor;
                    currentPage++;

                } while (cursor.HasValue && currentPage < maxPages);

                // Guardar en caché por 5 minutos
                _cache.Set(cacheKey, allPlayers, TimeSpan.FromMinutes(5));
                
                return allPlayers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching players from API: {SearchTerm}", searchTerm);
                return new List<PlayerSearchResult>();
            }
        }

        public async Task<List<PlayerSearchResult>> GetAllPlayersAsync(int perPage = 100, int maxPages = 2)
        {
            // Optimización: Cachear lista completa de jugadores (10 minutos)
            var cacheKey = $"all_players_{perPage}_{maxPages}";
            if (_cache.TryGetValue(cacheKey, out List<PlayerSearchResult>? cachedResults))
            {
                _logger.LogInformation("Lista completa de jugadores obtenida del caché");
                return cachedResults ?? new List<PlayerSearchResult>();
            }

            var allPlayers = new List<PlayerSearchResult>();
            
            try
            {
                // Reducir maxPages a 2 por defecto para mejorar velocidad
                for (int page = 1; page <= maxPages; page++)
                {
                    var response = await _httpClient.GetAsync($"players?per_page={perPage}&page={page}");
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Error en API al obtener jugadores: {StatusCode}", response.StatusCode);
                        break;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<PlayersResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Data == null || result.Data.Count == 0)
                        break;

                    allPlayers.AddRange(result.Data);
                    
                    // Si recibimos menos resultados que el límite, no hay más páginas
                    if (result.Data.Count < perPage)
                        break;
                }

                // Guardar en caché por 10 minutos
                _cache.Set(cacheKey, allPlayers, TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all players from API");
            }

            return allPlayers;
        }

        /// <summary>
        /// Obtiene las temporadas disponibles de la API.
        /// La API de balldontlie.io normalmente tiene datos desde 1979 hasta la temporada actual.
        /// </summary>
        public Task<List<int>> GetAvailableSeasonsAsync()
        {
            var cacheKey = "available_seasons";
            if (_cache.TryGetValue(cacheKey, out List<int>? cachedSeasons))
            {
                return Task.FromResult(cachedSeasons ?? new List<int>());
            }

            // La API de balldontlie.io tiene datos desde 1979 hasta la temporada actual
            // Generamos una lista de temporadas recientes (últimos 10 años)
            var currentYear = DateTime.Now.Year;
            var seasons = new List<int>();
            
            // Incluir temporadas desde 2015 hasta la actual
            for (int year = 2015; year <= currentYear; year++)
            {
                seasons.Add(year);
            }

            // Cachear por 1 hora (las temporadas no cambian frecuentemente)
            _cache.Set(cacheKey, seasons, TimeSpan.FromHours(1));
            
            return Task.FromResult<List<int>>(seasons);
        }

        public async Task<PlayerAverageStats?> GetPlayerStatsAsync(int playerId, int season = 2024)
        {
            // Optimización: Cachear estadísticas de jugadores (15 minutos)
            var cacheKey = $"player_stats_{playerId}_{season}";
            if (_cache.TryGetValue(cacheKey, out PlayerAverageStats? cachedStats))
            {
                _logger.LogInformation("Estadísticas obtenidas del caché para jugador {PlayerId}, temporada {Season}", playerId, season);
                return cachedStats;
            }

            try
            {
                // Obtener estadísticas de juegos individuales del jugador para la temporada
                // Reducir per_page a 50 para mejorar velocidad (suficiente para calcular promedios)
                var response = await _httpClient.GetAsync($"stats?seasons[]={season}&player_ids[]={playerId}&per_page=50");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Error en API al obtener estadísticas: {StatusCode} para jugador {PlayerId}", response.StatusCode, playerId);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GameStatsResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Data == null || !result.Data.Any())
                    return null;

                // Calcular promedios
                var stats = result.Data;
                var gamesPlayed = stats.Count;

                var averageStats = new PlayerAverageStats
                {
                    PlayerId = playerId,
                    Season = season,
                    GamesPlayed = gamesPlayed,
                    Pts = stats.Average(s => s.Pts),
                    Reb = stats.Average(s => s.Reb),
                    Ast = stats.Average(s => s.Ast),
                    Stl = stats.Average(s => s.Stl),
                    Blk = stats.Average(s => s.Blk),
                    Turnover = stats.Average(s => s.Turnover),
                    FgPct = stats.Where(s => s.Fga > 0).Average(s => (double)s.Fgm / s.Fga),
                    Fg3Pct = stats.Where(s => s.Fg3a > 0).Average(s => (double)s.Fg3m / s.Fg3a),
                    FtPct = stats.Where(s => s.Fta > 0).Average(s => (double)s.Ftm / s.Fta)
                };

                // Guardar en caché por 15 minutos
                _cache.Set(cacheKey, averageStats, TimeSpan.FromMinutes(15));
                
                return averageStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player stats from API para jugador {PlayerId}", playerId);
                return null;
            }
        }
    }

    // Modelos para la respuesta de la API
    public class PlayersResponse
    {
        [JsonPropertyName("data")]
        public List<PlayerSearchResult> Data { get; set; } = new();
    }

    public class PlayersResponseWithMeta
    {
        [JsonPropertyName("data")]
        public List<PlayerSearchResult> Data { get; set; } = new();

        [JsonPropertyName("meta")]
        public PaginationMeta? Meta { get; set; }
    }

    public class PaginationMeta
    {
        [JsonPropertyName("next_cursor")]
        public int? NextCursor { get; set; }

        [JsonPropertyName("per_page")]
        public int PerPage { get; set; }
    }

    public class PlayerSearchResult
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = "";

        [JsonPropertyName("last_name")]
        public string LastName { get; set; } = "";

        [JsonPropertyName("position")]
        public string Position { get; set; } = "";

        [JsonPropertyName("height")]
        public string? Height { get; set; }

        [JsonPropertyName("weight")]
        public string? Weight { get; set; }

        [JsonPropertyName("team")]
        public TeamInfo Team { get; set; } = new();

        public string FullName => $"{FirstName} {LastName}";
    }

    public class TeamInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = "";

        [JsonPropertyName("city")]
        public string City { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = "";
    }

    // Respuesta de estadísticas de juegos individuales
    public class GameStatsResponse
    {
        [JsonPropertyName("data")]
        public List<GamePlayerStats> Data { get; set; } = new();
    }

    public class GamePlayerStats
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("min")]
        public string? Min { get; set; }

        [JsonPropertyName("fgm")]
        public int Fgm { get; set; }

        [JsonPropertyName("fga")]
        public int Fga { get; set; }

        [JsonPropertyName("fg3m")]
        public int Fg3m { get; set; }

        [JsonPropertyName("fg3a")]
        public int Fg3a { get; set; }

        [JsonPropertyName("ftm")]
        public int Ftm { get; set; }

        [JsonPropertyName("fta")]
        public int Fta { get; set; }

        [JsonPropertyName("oreb")]
        public int Oreb { get; set; }

        [JsonPropertyName("dreb")]
        public int Dreb { get; set; }

        [JsonPropertyName("reb")]
        public int Reb { get; set; }

        [JsonPropertyName("ast")]
        public int Ast { get; set; }

        [JsonPropertyName("stl")]
        public int Stl { get; set; }

        [JsonPropertyName("blk")]
        public int Blk { get; set; }

        [JsonPropertyName("turnover")]
        public int Turnover { get; set; }

        [JsonPropertyName("pf")]
        public int Pf { get; set; }

        [JsonPropertyName("pts")]
        public int Pts { get; set; }
    }

    // Promedios calculados de estadísticas
    public class PlayerAverageStats
    {
        public int PlayerId { get; set; }
        public int Season { get; set; }
        public int GamesPlayed { get; set; }
        public double Pts { get; set; }
        public double Reb { get; set; }
        public double Ast { get; set; }
        public double Stl { get; set; }
        public double Blk { get; set; }
        public double Turnover { get; set; }
        public double FgPct { get; set; }
        public double Fg3Pct { get; set; }
        public double FtPct { get; set; }
    }
}
