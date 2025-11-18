using System.Reflection;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using NBADATA.Models;
using NBADATA.Data;    

namespace NBADATA.Pages
{
    [Authorize]
    public class CompareModel : PageModel
    {
    private readonly NBADbContext _db;
    private readonly IWebHostEnvironment _env;

    public CompareModel(NBADbContext db, IWebHostEnvironment env) => (_db, _env) = (db, env);

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
                // 1) Intento en DB (LIKE)
                Result1 = await _db.Players
                                   .Where(p => EF.Functions.Like(p.FullName, $"%{term}%"))
                                   .FirstOrDefaultAsync();

                // 2) Fallback en memoria con búsqueda más “permisiva”
                if (Result1 == null)
                {
                    var all = await _db.Players.ToListAsync();
                    Result1 = all.FirstOrDefault(p =>
                        p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
                }

                if (Result1 != null && !Players.Any(p => p.Id == Result1.Id))
                    Players.Add(Result1);

                DebugMsg += $" | Player1='{term}' -> {(Result1 != null ? Result1.FullName : "(not found)")}";
            }

            if (!string.IsNullOrWhiteSpace(Player2))
            {
                var term = Player2.Trim();
                Result2 = await _db.Players
                                   .Where(p => EF.Functions.Like(p.FullName, $"%{term}%"))
                                   .FirstOrDefaultAsync();

                if (Result2 == null)
                {
                    var all = await _db.Players.ToListAsync();
                    Result2 = all.FirstOrDefault(p =>
                        p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase));
                }

                if (Result2 != null && !Players.Any(p => p.Id == Result2.Id))
                    Players.Add(Result2);

                DebugMsg += $" | Player2='{term}' -> {(Result2 != null ? Result2.FullName : "(not found)")}";
            }


            var t = typeof(Player);
            Props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p =>
                        IsSimple(p.PropertyType) &&
                        p.Name != "Id") // excluye PK de la tabla
                     .OrderBy(p => p.Name)
                     .ToList();
            DeterminePhotoUrls();
        }

        private void DeterminePhotoUrls()
        {
            PhotoUrl1 = FindPhotoUrl(Result1);
            PhotoUrl2 = FindPhotoUrl(Result2);
        }

        // LOGICA PARA ENCONTRAR FOTO DE JUGADOR
        private string? FindPhotoUrl(Player? p)
        {
            if (p == null) return null;
            // localizacion de la carpeta wwwroot/images
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var imagesDir = Path.Combine(webRoot, "images");
            // funcion para normalizar los nombres (eliminar espacios y caracteres no alfanumericos)
            string Normalize(string name)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var ch in name)
                {
                    if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                }
                return sb.ToString();
            }
            // posibles nombres de los archivos
            var candidates = new List<string>();
            // por si los buscamos por su id
            candidates.Add($"{p.Id}.jpg");
            candidates.Add($"{p.Id}.png");
            // por si los buscamos por su nombre
            var nameNoSpaces = Normalize(p.FullName);
            candidates.Add(nameNoSpaces + ".png");
            candidates.Add(nameNoSpaces + ".jpg");
            // por si los buscamos por su nombre en minusculas
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
