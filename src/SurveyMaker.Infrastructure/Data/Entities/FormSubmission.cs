namespace SurveyMaker.Infrastructure.Data.Entities;

public class FormSubmission
{
    public Guid SubmissionId { get; set; }
    public Guid FormId { get; set; }
    public string? UserEmail { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public bool IsComplete { get; set; }
    public string? IpAddress { get; set; }

    public Form Form { get; set; } = null!;
    public ICollection<Answer> Answers { get; set; } = [];
}
