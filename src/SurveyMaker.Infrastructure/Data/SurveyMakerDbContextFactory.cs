using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SurveyMaker.Infrastructure.Data;

public class SurveyMakerDbContextFactory : IDesignTimeDbContextFactory<SurveyMakerDbContext>
{
    public SurveyMakerDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveApiPath();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. " +
                "Ensure appsettings.json is present in the API project.");

        var options = new DbContextOptionsBuilder<SurveyMakerDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SurveyMakerDbContext(options);
    }

    private static string ResolveApiPath()
    {
        // Walk up from the current directory to find the API project's appsettings.json
        var dir = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(dir, "src", "SurveyMaker.Api"),
            Path.Combine(dir, "..", "SurveyMaker.Api"),
            Path.Combine(dir, "..", "..", "src", "SurveyMaker.Api"),
            dir
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                return Path.GetFullPath(candidate);
        }

        return dir;
    }
}
