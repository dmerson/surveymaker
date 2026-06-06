namespace SurveyMaker.Infrastructure.Data.Entities;

public class Answer
{
    public int AnswerId { get; set; }
    public Guid SubmissionId { get; set; }
    public int QuestionId { get; set; }
    public string? AnswerScalar { get; set; }
    public string? AnswerJson { get; set; }

    public FormSubmission Submission { get; set; } = null!;
    public Question Question { get; set; } = null!;
}
