namespace SurveyMaker.Infrastructure.Data.Entities;

public class Question
{
    public int QuestionId { get; set; }
    public int SectionId { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = string.Empty;
    public int QuestionTypeId { get; set; }
    public string? QuestionAttributes { get; set; }

    public Section Section { get; set; } = null!;
    public QuestionType QuestionType { get; set; } = null!;
    public ICollection<Answer> Answers { get; set; } = [];
}
