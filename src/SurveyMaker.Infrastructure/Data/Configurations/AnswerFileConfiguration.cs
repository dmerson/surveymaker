using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Infrastructure.Data.Configurations;

public class AnswerFileConfiguration : IEntityTypeConfiguration<AnswerFile>
{
    public void Configure(EntityTypeBuilder<AnswerFile> builder)
    {
        builder.HasKey(f => f.FileId);
        builder.Property(f => f.FileId).HasDefaultValueSql("NEWID()");
        builder.Property(f => f.FileName).HasMaxLength(260).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(f => f.FileData).IsRequired();

        builder.HasOne(f => f.Submission)
            .WithMany()
            .HasForeignKey(f => f.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
