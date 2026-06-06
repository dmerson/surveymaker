using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Infrastructure.Data.Configurations;

public class SecurityTypeConfiguration : IEntityTypeConfiguration<SecurityType>
{
    public void Configure(EntityTypeBuilder<SecurityType> builder)
    {
        builder.HasKey(e => e.SecurityTypeId);
        builder.Property(e => e.SecurityTypeId).ValueGeneratedNever();
        builder.Property(e => e.SecurityTypeValue).IsRequired().HasMaxLength(25);

        builder.HasData(
            new SecurityType { SecurityTypeId = 1, SecurityTypeValue = "Public" },
            new SecurityType { SecurityTypeId = 2, SecurityTypeValue = "Private" },
            new SecurityType { SecurityTypeId = 3, SecurityTypeValue = "Url Allowed" }
        );
    }
}
