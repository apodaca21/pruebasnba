using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NBADATA.Data;

namespace NBADATA.Data
{
    public class NBADbContextFactory : IDesignTimeDbContextFactory<NBADbContext>
    {
        public NBADbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<NBADbContext>();

            // 1) Usa variable de entorno si está seteada 
            var conn = Environment.GetEnvironmentVariable("NBA_CONN");

            // 2) Si no hay env var, usa una conexión por defecto (SQLite)
            if (string.IsNullOrWhiteSpace(conn))
            {
                conn = "Data Source=nba.db"; 
                builder.UseSqlite(conn);
            }
            else
            {
                if (conn.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                    builder.UseSqlite(conn);
                else
                    builder.UseSqlite(conn);
            }

            return new NBADbContext(builder.Options);
        }
    }
}
