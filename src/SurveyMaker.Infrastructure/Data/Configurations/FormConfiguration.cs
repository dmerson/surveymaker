using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Infrastructure.Data.Configurations;

public class FormConfiguration : IEntityTypeConfiguration<Form>
{
    public void Configure(EntityTypeBuilder<Form> builder)
    {
        builder.HasKey(e => e.FormId);
        builder.Property(e => e.FormId).ValueGeneratedOnAdd();
        builder.Property(e => e.FormName).IsRequired().HasMaxLength(50);
        builder.Property(e => e.FormCreatorEmail).IsRequired().HasMaxLength(255);
        builder.Property(e => e.Description).HasMaxLength(255);
        builder.Property(e => e.Published).HasDefaultValue(false);

        builder.HasOne(e => e.SecurityType)
            .WithMany(e => e.Forms)
            .HasForeignKey(e => e.SecurityTypeId);

        builder.HasIndex(e => e.SecurityTypeId);
        builder.HasIndex(e => e.FormCreatorEmail);
    }
}
