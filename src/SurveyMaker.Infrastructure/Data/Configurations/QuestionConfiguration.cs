using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Infrastructure.Data.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasKey(e => e.QuestionId);
        builder.Property(e => e.Text).IsRequired().HasColumnName("Question");
        builder.Property(e => e.QuestionAttributes).HasColumnType("nvarchar(max)");

        builder.HasOne(e => e.Section)
            .WithMany(e => e.Questions)
            .HasForeignKey(e => e.SectionId);

        builder.HasOne(e => e.QuestionType)
            .WithMany(e => e.Questions)
            .HasForeignKey(e => e.QuestionTypeId);

        builder.HasIndex(e => e.SectionId);
        builder.HasIndex(e => e.QuestionTypeId);
    }
}
