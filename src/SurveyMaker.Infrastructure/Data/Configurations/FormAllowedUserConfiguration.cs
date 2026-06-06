using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Infrastructure.Data.Configurations;

public class FormAllowedUserConfiguration : IEntityTypeConfiguration<FormAllowedUser>
{
    public void Configure(EntityTypeBuilder<FormAllowedUser> builder)
    {
        builder.HasKey(e => e.FormAllowedUserId);
        builder.Property(e => e.UserEmail).IsRequired().HasMaxLength(255);

        builder.HasOne(e => e.Form)
            .WithMany(e => e.AllowedUsers)
            .HasForeignKey(e => e.FormId);

        builder.HasIndex(e => e.FormId);
    }
}
