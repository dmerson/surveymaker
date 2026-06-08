using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Infrastructure.Data.Configurations;

public class QuestionTypeConfiguration : IEntityTypeConfiguration<QuestionType>
{
    public void Configure(EntityTypeBuilder<QuestionType> builder)
    {
        builder.HasKey(e => e.QuestionTypeId);
        builder.Property(e => e.QuestionTypeId).ValueGeneratedNever();
        builder.Property(e => e.QuestionTypeName).IsRequired().HasMaxLength(50);

        builder.HasData(
            new QuestionType { QuestionTypeId = 1,  QuestionTypeName = "Text" },
            new QuestionType { QuestionTypeId = 2,  QuestionTypeName = "Long Text" },
            new QuestionType { QuestionTypeId = 3,  QuestionTypeName = "Number" },
            new QuestionType { QuestionTypeId = 4,  QuestionTypeName = "Radio Button" },
            new QuestionType { QuestionTypeId = 5,  QuestionTypeName = "Checkbox List" },
            new QuestionType { QuestionTypeId = 6,  QuestionTypeName = "Dropdown" },
            new QuestionType { QuestionTypeId = 7,  QuestionTypeName = "Date" },
            new QuestionType { QuestionTypeId = 8,  QuestionTypeName = "Time" },
            new QuestionType { QuestionTypeId = 9,  QuestionTypeName = "Date Time" },
            new QuestionType { QuestionTypeId = 10, QuestionTypeName = "Image" },
            new QuestionType { QuestionTypeId = 11, QuestionTypeName = "PDF" },
            new QuestionType { QuestionTypeId = 12, QuestionTypeName = "Rating Scale" },
            new QuestionType { QuestionTypeId = 13, QuestionTypeName = "Likert" },
            new QuestionType { QuestionTypeId = 14, QuestionTypeName = "Range" },
            new QuestionType { QuestionTypeId = 15, QuestionTypeName = "Email" },
            new QuestionType { QuestionTypeId = 16, QuestionTypeName = "Phone" },
            new QuestionType { QuestionTypeId = 17, QuestionTypeName = "Url" },
            new QuestionType { QuestionTypeId = 18, QuestionTypeName = "Net Promoter Score" },
            new QuestionType { QuestionTypeId = 19, QuestionTypeName = "Yes/No" },
            new QuestionType { QuestionTypeId = 20, QuestionTypeName = "Checklist With Number Values" },
            new QuestionType { QuestionTypeId = 21, QuestionTypeName = "Dropdown With Number Values" },
            new QuestionType { QuestionTypeId = 22, QuestionTypeName = "Radio With Number Values" },
            new QuestionType { QuestionTypeId = 23, QuestionTypeName = "Insert Previous Answer" },
            new QuestionType { QuestionTypeId = 24, QuestionTypeName = "Calculation" },
            new QuestionType { QuestionTypeId = 25, QuestionTypeName = "Graph" }
        );
    }
}
