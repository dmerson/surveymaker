using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SurveyMaker.Infrastructure.Data;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Api.Controllers;

[ApiController]
[Route("api/surveys")]
public class SurveysController(SurveyMakerDbContext db) : ControllerBase
{
    // ── My surveys (requires auth) ────────────────────────────────────────────

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();

        // Completed submissions (newest first)
        var completed = await db.FormSubmissions
            .Where(s => s.UserEmail == email && s.IsComplete)
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new
            {
                submissionId  = s.SubmissionId,
                formId        = s.FormId,
                formName      = s.Form.FormName,
                submittedAt   = s.SubmittedAt
            })
            .ToListAsync();

        // In-progress submissions (not yet complete, newest first)
        var inProgress = await db.FormSubmissions
            .Where(s => s.UserEmail == email && !s.IsComplete)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                submissionId = s.SubmissionId,
                formId       = s.FormId,
                formName     = s.Form.FormName,
                startedAt    = s.StartedAt
            })
            .ToListAsync();

        // Surveys explicitly assigned to this user
        var assigned = await db.FormAllowedUsers
            .Where(a => a.UserEmail == email && a.Form.Published)
            .OrderByDescending(a => a.Form.UpdatedAt)
            .Select(a => new
            {
                formId        = a.FormId,
                formName      = a.Form.FormName,
                description   = a.Form.Description,
                questionCount = a.Form.Sections.SelectMany(s => s.Questions).Count()
            })
            .ToListAsync();

        return Ok(new { completed, inProgress, assigned });
    }

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
        var callerEmail = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant()
            : null;

        var form = await db.Forms
            .Include(f => f.AllowedUsers)
            .Include(f => f.Sections.OrderBy(s => s.Order))
                .ThenInclude(s => s.Questions.OrderBy(q => q.Order))
                    .ThenInclude(q => q.QuestionType)
            .FirstOrDefaultAsync(f => f.FormId == formId);

        if (form is null) return NotFound();

        // Unpublished forms are only visible to their creator (preview mode)
        var creatorEmail = form.FormCreatorEmail?.Trim().ToLowerInvariant();
        if (!form.Published && creatorEmail != callerEmail)
            return NotFound();

        // Private: must be authenticated and in the allowed list (or be creator)
        if (form.SecurityTypeId == 2)
        {
            if (callerEmail is null) return Forbid();
            var allowed = creatorEmail == callerEmail
                       || form.AllowedUsers.Any(u => u.UserEmail == callerEmail);
            if (!allowed) return Forbid();
        }

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
        var callerEmail = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant()
            : null;

        var form = await db.Forms
            .Include(f => f.AllowedUsers)
            .FirstOrDefaultAsync(f => f.FormId == formId);
        if (form is null) return NotFound();

        var creatorEmail = form.FormCreatorEmail?.Trim().ToLowerInvariant();

        // Unpublished: only the creator can submit (preview/testing mode)
        if (!form.Published && creatorEmail != callerEmail)
            return NotFound();

        if (form.SecurityTypeId == 2)
        {
            if (callerEmail is null) return Forbid();
            var allowed = creatorEmail == callerEmail
                       || form.AllowedUsers.Any(u => u.UserEmail == callerEmail);
            if (!allowed) return Forbid();
        }

        var submission = new FormSubmission
        {
            FormId      = form.FormId,
            UserEmail   = callerEmail,
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
