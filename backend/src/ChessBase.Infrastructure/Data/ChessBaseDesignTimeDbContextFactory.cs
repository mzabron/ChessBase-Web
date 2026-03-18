using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ChessBase.Infrastructure.Data;

public class ChessBaseDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ChessBaseDbContext>
{
    public ChessBaseDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChessBaseDbContext>();

        var apiProjectDirectory = ResolveApiProjectDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProjectDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CHESSBASE_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Unable to resolve connection string for design-time EF operations. " +
                "Set ConnectionStrings:DefaultConnection in src/ChessBase.Api/appsettings.json " +
                "or set CHESSBASE_CONNECTION_STRING in environment variables.");
        }

        optionsBuilder.UseNpgsql(connectionString);
        return new ChessBaseDbContext(optionsBuilder.Options);
    }

    private static string ResolveApiProjectDirectory()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var start in candidates)
        {
            var current = new DirectoryInfo(start);
            while (current is not null)
            {
                var apiPath = Path.Combine(current.FullName, "src", "ChessBase.Api");
                if (Directory.Exists(apiPath))
                {
                    return apiPath;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not locate src/ChessBase.Api directory for loading appsettings.json during design-time DbContext creation.");
    }
}
