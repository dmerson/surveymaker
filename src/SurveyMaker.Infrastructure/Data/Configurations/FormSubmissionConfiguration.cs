using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Infrastructure.Data.Configurations;

public class FormSubmissionConfiguration : IEntityTypeConfiguration<FormSubmission>
{
    public void Configure(EntityTypeBuilder<FormSubmission> builder)
    {
        builder.HasKey(e => e.SubmissionId);
        builder.Property(e => e.SubmissionId).ValueGeneratedOnAdd();
        builder.Property(e => e.UserEmail).HasMaxLength(255);
        builder.Property(e => e.IpAddress).HasMaxLength(45);

        builder.HasOne(e => e.Form)
            .WithMany(e => e.Submissions)
            .HasForeignKey(e => e.FormId);

        builder.HasIndex(e => e.FormId);
        builder.HasIndex(e => e.UserEmail);
    }
}
