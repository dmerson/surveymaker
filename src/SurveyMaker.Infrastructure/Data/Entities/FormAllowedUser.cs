namespace SurveyMaker.Infrastructure.Data.Entities;

public class FormAllowedUser
{
    public int FormAllowedUserId { get; set; }
    public Guid FormId { get; set; }
    public string UserEmail { get; set; } = string.Empty;

    public Form Form { get; set; } = null!;
}
