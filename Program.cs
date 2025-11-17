// Program.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NBADATA.Data;
using NBADATA.Models;

var builder = WebApplication.CreateBuilder(args);

// 1) EF Core con tu provider elegido
builder.Services.AddDbContext<NBADbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// 2) Identity (usuarios)
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(o =>
{
o.Password.RequiredLength = 6;
o.Password.RequireDigit = true;
o.Password.RequireNonAlphanumeric = false;
})
    .AddEntityFrameworkStores<NBADbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.ExpireTimeSpan = TimeSpan.FromDays(14);
    opt.SlidingExpiration = true;
    opt.LoginPath = "/Account/Login";
    opt.LogoutPath = "/Account/Logout";
    opt.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddRazorPages();
builder.Services.AddControllers();

var app = builder.Build();

// 3) Middleware
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
app.MapControllers();

// 4) Seed opcional (solo si la tabla está vacía)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NBADbContext>();
    db.Database.Migrate(); // aplica migraciones en arranque

    if (!db.Players.Any())
    {
        db.Players.AddRange(
            new Player { FullName = "LeBron James", Team = "LAL", Position = "F", HeightCm = 206, WeightKg = 113, BirthDate = new DateTime(1984, 12, 30), Pts = 25.3, Reb = 7.4, Ast = 7.9, Stl = 1.1, Blk = 0.5, Tov = 3.2, FgPct = 0.525, TpPct = 0.367, FtPct = 0.750 },
            new Player { FullName = "Stephen Curry", Team = "GSW", Position = "G", HeightCm = 188, WeightKg = 84, BirthDate = new DateTime(1988, 3, 14), Pts = 27.3, Reb = 4.5, Ast = 6.2, Stl = 1.0, Blk = 0.4, Tov = 3.1, FgPct = 0.487, TpPct = 0.421, FtPct = 0.915 },
            new Player { Id = 3, FullName = "Nikola Jokić", Team = "DEN", Position = "C", HeightCm = 211, WeightKg = 129, BirthDate = new DateTime(1995, 2, 19), Pts = 26.4, Reb = 12.4, Ast = 9.0, Stl = 1.2, Blk = 0.8, Tov = 3.4, FgPct = 0.580, TpPct = 0.370, FtPct = 0.830 },
            new Player { Id = 4, FullName = "Luka Dončić", Team = "DAL", Position = "G", HeightCm = 201, WeightKg = 104, BirthDate = new DateTime(1999, 2, 28), Pts = 28.4, Reb = 8.7, Ast = 8.7, Stl = 1.1, Blk = 0.5, Tov = 4.0, FgPct = 0.459, TpPct = 0.346, FtPct = 0.743 },
            new Player { Id = 5, FullName = "Giannis Antetokounmpo", Team = "MIL", Position = "F", HeightCm = 211, WeightKg = 110, BirthDate = new DateTime(1994, 12, 6), Pts = 31.1, Reb = 11.8, Ast = 5.7, Stl = 0.8, Blk = 0.8, Tov = 3.4, FgPct = 0.553, TpPct = 0.275, FtPct = 0.656 },
            new Player { Id = 6, FullName = "Jayson Tatum", Team = "BOS", Position = "F", HeightCm = 203, WeightKg = 95, BirthDate = new DateTime(1998, 3, 3), Pts = 30.1, Reb = 8.8, Ast = 4.6, Stl = 1.1, Blk = 0.7, Tov = 2.9, FgPct = 0.466, TpPct = 0.350, FtPct = 0.854 },
            new Player { Id = 7, FullName = "Joel Embiid", Team = "PHI", Position = "C", HeightCm = 213, WeightKg = 127, BirthDate = new DateTime(1994, 3, 16), Pts = 33.1, Reb = 10.2, Ast = 4.2, Stl = 1.0, Blk = 1.7, Tov = 3.4, FgPct = 0.548, TpPct = 0.330, FtPct = 0.857 },
            new Player { Id = 8, FullName = "Kevin Durant", Team = "PHX", Position = "F", HeightCm = 208, WeightKg = 109, BirthDate = new DateTime(1988, 9, 29), Pts = 29.1, Reb = 6.7, Ast = 5.0, Stl = 0.9, Blk = 1.2, Tov = 3.3, FgPct = 0.559, TpPct = 0.404, FtPct = 0.918 },
            new Player { Id = 9, FullName = "Damian Lillard", Team = "MIL", Position = "G", HeightCm = 188, WeightKg = 88, BirthDate = new DateTime(1990, 7, 15), Pts = 25.1, Reb = 4.2, Ast = 6.8, Stl = 1.0, Blk = 0.3, Tov = 2.9, FgPct = 0.424, TpPct = 0.351, FtPct = 0.914 },
            new Player { Id = 10, FullName = "Anthony Davis", Team = "LAL", Position = "F-C", HeightCm = 208, WeightKg = 115, BirthDate = new DateTime(1993, 3, 11), Pts = 24.1, Reb = 12.6, Ast = 3.5, Stl = 1.2, Blk = 2.3, Tov = 2.6, FgPct = 0.563, TpPct = 0.259, FtPct = 0.784 }
        );
        db.SaveChanges();
    }
}

// 5) Endpoint API para búsqueda de jugadores
app.MapGet("/api/players/search", async (string? query, NBADbContext db) =>
{
    if (string.IsNullOrWhiteSpace(query))
        return Results.Ok(Array.Empty<object>());

    query = query.Trim();

    var matches = await db.Players
        .Where(p => EF.Functions.Like(p.FullName, $"%{query}%"))
        .OrderBy(p => p.FullName)
        .Take(10)
        .Select(p => new
        {
            id = p.Id,
            fullName = p.FullName,
            team = p.Team
        })
        .ToListAsync();

    return Results.Ok(matches);
});


app.Run();
