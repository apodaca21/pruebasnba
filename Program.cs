using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NBADATA.Data;
using NBADATA.Models;
using NBADATA.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<NBADbContext>(opt =>
    opt.UseInMemoryDatabase("nba"));

// Configurar Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<NBADbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

// Registrar el servicio de NBA API
builder.Services.AddHttpClient<NBAApiService>();

builder.Services.AddRazorPages();

var app = builder.Build();


// Agregamos juadores de ejemplo
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NBADbContext>();
    if (!db.Players.Any())
    {
        db.Players.AddRange(
            new Player { Id = 1, FullName = "LeBron James", Team = "LAL", Position = "F", HeightCm = 206, WeightKg = 113, BirthDate = new DateTime(1984, 12, 30), Pts = 25.3, Reb = 7.4, Ast = 7.9, Stl = 1.1, Blk = 0.5, Tov = 3.2, FgPct = 0.525, TpPct = 0.367, FtPct = 0.750 },
            new Player { Id = 2, FullName = "Stephen Curry", Team = "GSW", Position = "G", HeightCm = 188, WeightKg = 84, BirthDate = new DateTime(1988, 3, 14), Pts = 27.3, Reb = 4.5, Ast = 6.2, Stl = 1.0, Blk = 0.4, Tov = 3.1, FgPct = 0.487, TpPct = 0.421, FtPct = 0.915 },
            new Player { Id = 3, FullName = "Nikola Jokić", Team = "DEN", Position = "C", HeightCm = 211, WeightKg = 129, BirthDate = new DateTime(1995, 2, 19), Pts = 26.4, Reb = 12.4, Ast = 9.0, Stl = 1.2, Blk = 0.8, Tov = 3.4, FgPct = 0.580, TpPct = 0.370, FtPct = 0.830 }
        );
        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Endpoint para buscar jugadores en la API externa
app.MapGet("/api/nba/search", async (string? query, NBAApiService nbaApi) =>
{
    if (string.IsNullOrWhiteSpace(query))
        return Results.Ok(Array.Empty<object>());

    var players = await nbaApi.SearchPlayersAsync(query);
    
    var results = players.Select(p => new
    {
        id = p.Id,
        fullName = p.FullName,
        team = p.Team.Abbreviation,
        position = p.Position
    }).ToList();

    return Results.Ok(results);
});

// Endpoint para obtener estadísticas de un jugador
app.MapGet("/api/nba/stats/{playerId}", async (int playerId, NBAApiService nbaApi) =>
{
    var stats = await nbaApi.GetPlayerStatsAsync(playerId);
    
    if (stats == null)
        return Results.NotFound();

    return Results.Ok(stats);
});

// Endpoint de prueba para debugging
app.MapGet("/api/nba/test", async (string? query, NBAApiService nbaApi) =>
{
    if (string.IsNullOrWhiteSpace(query))
        return Results.Json(new { error = "Query requerido" });

    try
    {
        var results = await nbaApi.SearchPlayersAsync(query);
        return Results.Json(new 
        { 
            totalResults = results.Count,
            query = query,
            players = results.Select(p => new
            {
                id = p.Id,
                fullName = p.FullName,
                firstName = p.FirstName,
                lastName = p.LastName,
                team = p.Team.Abbreviation,
                position = p.Position
            }).ToList()
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message, stack = ex.StackTrace });
    }
});

app.Run();