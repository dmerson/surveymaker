using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SurveyMaker.Infrastructure.Identity;

namespace SurveyMaker.Infrastructure.Data;

public class SurveyMakerDbContext(DbContextOptions<SurveyMakerDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SurveyMakerDbContext).Assembly);
    }
}
