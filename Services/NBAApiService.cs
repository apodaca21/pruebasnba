using System.Text.Json;
using System.Text.Json.Serialization;

namespace NBADATA.Services
{
    public class NBAApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NBAApiService> _logger;

        public NBAApiService(HttpClient httpClient, ILogger<NBAApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("https://api.balldontlie.io/v1/");
            _httpClient.DefaultRequestHeaders.Add("Authorization", "05ea5d28-7031-4b22-b3d2-b826840e0529");
        }

        public async Task<List<PlayerSearchResult>> SearchPlayersAsync(string searchTerm)
        {
            try
            {
                var allPlayers = new List<PlayerSearchResult>();
                int? cursor = null;
                int maxPages = 3; // Buscar en 3 páginas (75 jugadores)
                int currentPage = 0;

                do
                {
                    var url = cursor.HasValue 
                        ? $"players?search={Uri.EscapeDataString(searchTerm)}&cursor={cursor}&per_page=25"
                        : $"players?search={Uri.EscapeDataString(searchTerm)}&per_page=25";
                    
                    var response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

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

                return allPlayers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching players from API");
                return new List<PlayerSearchResult>();
            }
        }

        public async Task<List<PlayerSearchResult>> GetAllPlayersAsync(int perPage = 100, int maxPages = 5)
        {
            var allPlayers = new List<PlayerSearchResult>();
            
            try
            {
                for (int page = 1; page <= maxPages; page++)
                {
                    var response = await _httpClient.GetAsync($"players?per_page={perPage}&page={page}");
                    response.EnsureSuccessStatusCode();

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all players from API");
            }

            return allPlayers;
        }

        public async Task<PlayerAverageStats?> GetPlayerStatsAsync(int playerId, int season = 2024)
        {
            try
            {
                // Obtener estadísticas de juegos individuales del jugador para la temporada
                var response = await _httpClient.GetAsync($"stats?seasons[]={season}&player_ids[]={playerId}&per_page=100");
                response.EnsureSuccessStatusCode();

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

                return new PlayerAverageStats
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player stats from API");
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
