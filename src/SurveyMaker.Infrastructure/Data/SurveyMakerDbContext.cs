using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SurveyMaker.Infrastructure.Data.Entities;
using SurveyMaker.Infrastructure.Identity;

namespace SurveyMaker.Infrastructure.Data;

public class SurveyMakerDbContext(DbContextOptions<SurveyMakerDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<SecurityType> SecurityTypes => Set<SecurityType>();
    public DbSet<Form> Forms => Set<Form>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<QuestionType> QuestionTypes => Set<QuestionType>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<AnswerFile> AnswerFiles => Set<AnswerFile>();
    public DbSet<FormAllowedUser> FormAllowedUsers => Set<FormAllowedUser>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SurveyMakerDbContext).Assembly);
    }
}
