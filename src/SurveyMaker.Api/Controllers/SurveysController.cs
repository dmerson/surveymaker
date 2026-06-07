using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SurveyMaker.Infrastructure.Data;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Api.Controllers;

[ApiController]
[Route("api/surveys")]
public class SurveysController(SurveyMakerDbContext db) : ControllerBase
{
    // ── List public surveys ───────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> ListPublic()
    {
        var surveys = await db.Forms
            .Where(f => f.Published && f.SecurityTypeId == 1)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                formId        = f.FormId,
                formName      = f.FormName,
                description   = f.Description,
                questionCount = f.Sections.SelectMany(s => s.Questions).Count(),
                createdAt     = f.CreatedAt
            })
            .ToListAsync();

        return Ok(surveys);
    }

    // ── Survey detail ─────────────────────────────────────────────────────────

    [HttpGet("{formId:guid}")]
    public async Task<IActionResult> GetSurvey(Guid formId)
    {
        var form = await db.Forms
            .Include(f => f.Sections.OrderBy(s => s.Order))
                .ThenInclude(s => s.Questions.OrderBy(q => q.Order))
                    .ThenInclude(q => q.QuestionType)
            .FirstOrDefaultAsync(f => f.FormId == formId
                                   && f.Published
                                   && f.SecurityTypeId == 1);

        if (form is null) return NotFound();

        return Ok(new
        {
            formId      = form.FormId,
            formName    = form.FormName,
            description = form.Description,
            sections    = form.Sections.Select(s => new
            {
                sectionId   = s.SectionId,
                sectionName = s.SectionName,
                order       = s.Order,
                questions   = s.Questions.Select(q => new
                {
                    questionId         = q.QuestionId,
                    order              = q.Order,
                    text               = q.Text,
                    questionTypeId     = q.QuestionTypeId,
                    questionTypeName   = q.QuestionType.QuestionTypeName,
                    questionAttributes = q.QuestionAttributes
                })
            })
        });
    }

    // ── Submit survey ─────────────────────────────────────────────────────────

    [HttpPost("{formId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid formId, [FromBody] SurveySubmitRequest request)
    {
        var formExists = await db.Forms
            .AnyAsync(f => f.FormId == formId && f.Published && f.SecurityTypeId == 1);
        if (!formExists) return NotFound();

        var userEmail = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.Email)
            : null;

        var submission = new FormSubmission
        {
            FormId      = formId,
            UserEmail   = userEmail,
            StartedAt   = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow,
            IsComplete  = true
        };
        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync();

        var answers = request.Answers
            .Where(a => !string.IsNullOrWhiteSpace(a.AnswerScalar)
                     || !string.IsNullOrWhiteSpace(a.AnswerJson))
            .Select(a => new Answer
            {
                SubmissionId  = submission.SubmissionId,
                QuestionId    = a.QuestionId,
                AnswerScalar  = a.AnswerScalar?.Trim(),
                AnswerJson    = a.AnswerJson
            })
            .ToList();

        if (answers.Count > 0)
        {
            db.Answers.AddRange(answers);
            await db.SaveChangesAsync();
        }

        return Ok(new { submissionId = submission.SubmissionId });
    }
}

public record SurveyAnswerItem(int QuestionId, string? AnswerScalar, string? AnswerJson);
public record SurveySubmitRequest(List<SurveyAnswerItem> Answers);
