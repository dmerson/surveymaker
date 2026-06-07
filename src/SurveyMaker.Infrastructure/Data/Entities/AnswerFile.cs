namespace SurveyMaker.Infrastructure.Data.Entities;

public class AnswerFile
{
    public Guid FileId { get; set; }
    public Guid SubmissionId { get; set; }
    public int QuestionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] FileData { get; set; } = [];
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }

    public FormSubmission Submission { get; set; } = null!;
}
