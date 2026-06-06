using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Infrastructure.Data.Configurations;

public class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.HasKey(e => e.AnswerId);
        builder.Property(e => e.AnswerScalar).HasColumnType("nvarchar(max)");
        builder.Property(e => e.AnswerJson).HasColumnType("nvarchar(max)");

        builder.HasOne(e => e.Submission)
            .WithMany(e => e.Answers)
            .HasForeignKey(e => e.SubmissionId);

        builder.HasOne(e => e.Question)
            .WithMany(e => e.Answers)
            .HasForeignKey(e => e.QuestionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(e => e.SubmissionId);
        builder.HasIndex(e => e.QuestionId);
    }
}
