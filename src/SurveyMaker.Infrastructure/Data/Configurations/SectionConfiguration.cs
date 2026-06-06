using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Infrastructure.Data.Configurations;

public class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.HasKey(e => e.SectionId);
        builder.Property(e => e.SectionName).HasMaxLength(50);

        builder.HasOne(e => e.Form)
            .WithMany(e => e.Sections)
            .HasForeignKey(e => e.FormId);

        builder.HasIndex(e => e.FormId);
    }
}
