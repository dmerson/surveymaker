namespace SurveyMaker.Infrastructure.Data.Entities;

public class Section
{
    public int SectionId { get; set; }
    public Guid FormId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public bool IsMatrix { get; set; }
    public int Order { get; set; }
    public bool ShowAsPage { get; set; }

    public Form Form { get; set; } = null!;
    public ICollection<Question> Questions { get; set; } = [];
}
