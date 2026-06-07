using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SurveyMaker.Infrastructure.Data;

namespace SurveyMaker.IntegrationTests;

/// <summary>
/// Spins up the full ASP.NET Core pipeline with:
///   - SQL Server replaced by EF Core InMemory (no connection string required)
///   - Fake Google OAuth credentials so the app starts without User Secrets
///   - Development environment so auth cookies use SameSite=Lax
/// One factory instance is shared per test class (IClassFixture) so all tests
/// in a class share the same in-memory database.
/// </summary>
public class SurveyMakerWebFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "Integration_" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Inject fake Google credentials so the app starts without real User Secrets
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Google:ClientId"]     = "test-client-id",
                ["Authentication:Google:ClientSecret"] = "test-client-secret"
            }));

        // Swap SQL Server for EF Core InMemory.
        // Must remove both DbContextOptions<T> AND the internal IDbContextOptionsConfiguration<T>
        // descriptors, otherwise both the SqlServer and InMemory providers end up registered and
        // EF Core throws "Only a single database provider can be registered".
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<SurveyMakerDbContext>)
                    || (d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration")
                        && d.ServiceType.GetGenericArguments() is [var arg]
                        && arg == typeof(SurveyMakerDbContext)))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<SurveyMakerDbContext>(opts =>
                opts.UseInMemoryDatabase(_dbName));
        });
    }

    public void Seed(Action<SurveyMakerDbContext> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SurveyMakerDbContext>();
        action(db);
    }
}
