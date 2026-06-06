namespace SurveyMaker.Infrastructure.Data.Entities;

public class SecurityType
{
    public int SecurityTypeId { get; set; }
    public string SecurityTypeValue { get; set; } = string.Empty;

    public ICollection<Form> Forms { get; set; } = [];
}
