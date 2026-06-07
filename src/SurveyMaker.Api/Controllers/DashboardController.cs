using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SurveyMaker.Infrastructure.Data;

namespace SurveyMaker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(SurveyMakerDbContext db) : ControllerBase
{
    private string UserEmail =>
        User.FindFirstValue(ClaimTypes.Email)!;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var toDay   = to   ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDay = from ?? toDay.AddDays(-29);

        var fromDt = fromDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDt   = toDay.ToDateTime(TimeOnly.MaxValue,   DateTimeKind.Utc);

        // All forms owned by this user
        var myForms = await db.Forms
            .Where(f => f.FormCreatorEmail == UserEmail)
            .Select(f => new
            {
                f.FormId,
                f.FormName,
                f.SecurityTypeId,
                f.Published,
                f.CreatedAt
            })
            .ToListAsync();

        var myFormIds = myForms.Select(f => f.FormId).ToList();

        // Forms created in the date range
        var formsInRange = myForms
            .Where(f => f.CreatedAt >= fromDt && f.CreatedAt <= toDt)
            .OrderByDescending(f => f.CreatedAt)
            .ToList();

        // Submissions submitted in the date range (across all owned forms)
        var activity = await db.FormSubmissions
            .Where(s => myFormIds.Contains(s.FormId)
                     && s.SubmittedAt.HasValue
                     && s.SubmittedAt >= fromDt
                     && s.SubmittedAt <= toDt)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new
            {
                submissionId = s.SubmissionId,
                formId       = s.FormId,
                formName     = s.Form.FormName,
                userEmail    = s.UserEmail,
                submittedAt  = s.SubmittedAt,
                isComplete   = s.IsComplete
            })
            .ToListAsync();

        return Ok(new
        {
            myFormsCount      = formsInRange.Count,
            responsesReceived = activity.Count,
            surveysCompleted  = activity.Count(a => a.isComplete),
            recentForms       = formsInRange.Take(5),
            recentActivity    = activity.Take(20)
        });
    }
}
