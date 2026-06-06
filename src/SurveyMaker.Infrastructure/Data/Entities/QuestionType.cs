namespace SurveyMaker.Infrastructure.Data.Entities;

public class QuestionType
{
    public int QuestionTypeId { get; set; }
    public string QuestionTypeName { get; set; } = string.Empty;

    public ICollection<Question> Questions { get; set; } = [];
}
