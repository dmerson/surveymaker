namespace SurveyMaker.Infrastructure.Data.Entities;

public class Form
{
    public Guid FormId { get; set; }
    public string FormName { get; set; } = string.Empty;
    public string FormCreatorEmail { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SecurityTypeId { get; set; }
    public bool RandomizeOrder { get; set; }
    public bool Published { get; set; }
    public int? Quota { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public SecurityType SecurityType { get; set; } = null!;
    public ICollection<Section> Sections { get; set; } = [];
    public ICollection<FormAllowedUser> AllowedUsers { get; set; } = [];
    public ICollection<FormSubmission> Submissions { get; set; } = [];
}
