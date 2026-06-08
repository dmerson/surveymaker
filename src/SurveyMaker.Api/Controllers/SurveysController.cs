using System.Security.Claims;
using System.Text.Json;
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
    private static readonly HashSet<int> FileTypeIds = [10, 11];
    private const long MaxFileBytes = 4_718_592L; // 4.5 MB
    private const int MaxFileQuota  = 25;

    // ── My surveys (requires auth) ────────────────────────────────────────────

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();

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

        var assigned = await db.FormAllowedUsers
            .Where(a => a.UserEmail.ToLower() == email && a.Form.Published)
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

        var creatorEmail = form.FormCreatorEmail?.Trim().ToLowerInvariant();
        if (!form.Published && creatorEmail != callerEmail)
            return NotFound();

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

    // ── Load progress ────────────────────────────────────────────────────────

    [HttpGet("{formId:guid}/progress")]
    [Authorize]
    public async Task<IActionResult> LoadProgress(Guid formId)
    {
        var email = User.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();

        var existing = await db.FormSubmissions
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.FormId == formId && s.UserEmail == email && !s.IsComplete);

        if (existing is null)
            return Ok(new { submissionId = (string?)null, answers = Array.Empty<object>() });

        var answers = existing.Answers.Select(a => new
        {
            questionId   = a.QuestionId,
            answerScalar = a.AnswerScalar,
            answerJson   = a.AnswerJson
        });

        return Ok(new { submissionId = (string?)existing.SubmissionId.ToString(), answers });
    }

    // ── Save progress ─────────────────────────────────────────────────────────

    [HttpPost("{formId:guid}/progress")]
    [Authorize]
    public async Task<IActionResult> SaveProgress(Guid formId, [FromBody] SurveySubmitRequest request)
    {
        var email = User.FindFirstValue(ClaimTypes.Email)!.Trim().ToLowerInvariant();

        var form = await db.Forms
            .Include(f => f.AllowedUsers)
            .Include(f => f.Sections)
                .ThenInclude(s => s.Questions)
            .FirstOrDefaultAsync(f => f.FormId == formId);
        if (form is null) return NotFound();

        var creatorEmail = form.FormCreatorEmail?.Trim().ToLowerInvariant();

        if (!form.Published && creatorEmail != email) return NotFound();

        if (form.SecurityTypeId == 2)
        {
            var allowed = creatorEmail == email
                       || form.AllowedUsers.Any(u => u.UserEmail == email);
            if (!allowed) return Forbid();
        }

        var questionTypeMap = form.Sections
            .SelectMany(s => s.Questions)
            .ToDictionary(q => q.QuestionId, q => q.QuestionTypeId);

        if (questionTypeMap.Values.Any(t => FileTypeIds.Contains(t)))
            return BadRequest(new { error = "Save progress is not available for forms with file upload questions." });

        var existing = await db.FormSubmissions
            .FirstOrDefaultAsync(s => s.FormId == formId && s.UserEmail == email && !s.IsComplete);

        FormSubmission submission;
        if (existing is not null)
        {
            var oldAnswers = await db.Answers
                .Where(a => a.SubmissionId == existing.SubmissionId).ToListAsync();
            db.Answers.RemoveRange(oldAnswers);
            await db.SaveChangesAsync();
            submission = existing;
        }
        else
        {
            submission = new FormSubmission
            {
                FormId    = form.FormId,
                UserEmail = email,
                StartedAt = DateTime.UtcNow,
                IsComplete = false
            };
            db.FormSubmissions.Add(submission);
            await db.SaveChangesAsync();
        }

        var answers = new List<Answer>();
        foreach (var a in request.Answers)
        {
            if (string.IsNullOrWhiteSpace(a.AnswerScalar) && string.IsNullOrWhiteSpace(a.AnswerJson))
                continue;
            questionTypeMap.TryGetValue(a.QuestionId, out var typeId);
            if (FileTypeIds.Contains(typeId)) continue;
            answers.Add(new Answer
            {
                SubmissionId = submission.SubmissionId,
                QuestionId   = a.QuestionId,
                AnswerScalar = a.AnswerScalar?.Trim(),
                AnswerJson   = a.AnswerJson
            });
        }

        if (answers.Count > 0)
        {
            db.Answers.AddRange(answers);
            await db.SaveChangesAsync();
        }

        return Ok(new { submissionId = submission.SubmissionId });
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
            .Include(f => f.Sections)
                .ThenInclude(s => s.Questions)
            .FirstOrDefaultAsync(f => f.FormId == formId);
        if (form is null) return NotFound();

        var creatorEmail = form.FormCreatorEmail?.Trim().ToLowerInvariant();

        if (!form.Published && creatorEmail != callerEmail)
            return NotFound();

        if (form.SecurityTypeId == 2)
        {
            if (callerEmail is null) return Forbid();
            var allowed = creatorEmail == callerEmail
                       || form.AllowedUsers.Any(u => u.UserEmail == callerEmail);
            if (!allowed) return Forbid();
        }

        // Build question-type lookup for file handling
        var questionTypeMap = form.Sections
            .SelectMany(s => s.Questions)
            .ToDictionary(q => q.QuestionId, q => q.QuestionTypeId);

        bool hasFileQuestions = questionTypeMap.Values.Any(t => FileTypeIds.Contains(t));

        // Forms with file questions must be private with quota ≤ 25
        if (hasFileQuestions)
        {
            if (form.SecurityTypeId != 2)
                return BadRequest(new { error = "File upload questions are only available on private forms." });
            if (!form.Quota.HasValue || form.Quota.Value > MaxFileQuota)
                return BadRequest(new { error = $"Forms with file questions require a quota of {MaxFileQuota} or fewer." });
        }

        // Enforce quota
        if (form.Quota.HasValue)
        {
            var count = await db.FormSubmissions.CountAsync(s => s.FormId == formId && s.IsComplete);
            if (count >= form.Quota.Value)
                return BadRequest(new { error = "This survey has reached its response limit." });
        }

        // Convert existing in-progress submission, or create a new one
        var existing = callerEmail is not null
            ? await db.FormSubmissions.FirstOrDefaultAsync(
                s => s.FormId == formId && s.UserEmail == callerEmail && !s.IsComplete)
            : null;

        FormSubmission submission;
        if (existing is not null)
        {
            var oldAnswers = await db.Answers
                .Where(a => a.SubmissionId == existing.SubmissionId).ToListAsync();
            db.Answers.RemoveRange(oldAnswers);
            existing.SubmittedAt = DateTime.UtcNow;
            existing.IsComplete  = true;
            await db.SaveChangesAsync();
            submission = existing;
        }
        else
        {
            submission = new FormSubmission
            {
                FormId      = form.FormId,
                UserEmail   = callerEmail,
                StartedAt   = DateTime.UtcNow,
                SubmittedAt = DateTime.UtcNow,
                IsComplete  = true
            };
            db.FormSubmissions.Add(submission);
            await db.SaveChangesAsync();
        }

        var answers     = new List<Answer>();
        var answerFiles = new List<AnswerFile>();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        foreach (var a in request.Answers)
        {
            if (string.IsNullOrWhiteSpace(a.AnswerScalar) && string.IsNullOrWhiteSpace(a.AnswerJson))
                continue;

            questionTypeMap.TryGetValue(a.QuestionId, out var typeId);

            if (FileTypeIds.Contains(typeId) && !string.IsNullOrWhiteSpace(a.AnswerJson))
            {
                FilePayload? payload;
                try { payload = JsonSerializer.Deserialize<FilePayload>(a.AnswerJson, jsonOptions); }
                catch { continue; }

                if (payload is null || string.IsNullOrWhiteSpace(payload.Data)) continue;

                bool validType = typeId == 10
                    ? payload.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    : payload.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

                if (!validType)
                    return BadRequest(new { error = $"Invalid file type for question {a.QuestionId}." });

                byte[] fileBytes;
                try { fileBytes = Convert.FromBase64String(payload.Data); }
                catch { return BadRequest(new { error = $"Invalid file data for question {a.QuestionId}." }); }

                if (fileBytes.Length > MaxFileBytes)
                    return BadRequest(new { error = $"File for question {a.QuestionId} exceeds the 4.5 MB limit." });

                var fileId = Guid.NewGuid();
                answerFiles.Add(new AnswerFile
                {
                    FileId        = fileId,
                    SubmissionId  = submission.SubmissionId,
                    QuestionId    = a.QuestionId,
                    FileName      = payload.FileName ?? "upload",
                    ContentType   = payload.ContentType,
                    FileData      = fileBytes,
                    FileSizeBytes = fileBytes.Length,
                    UploadedAt    = DateTime.UtcNow
                });
                answers.Add(new Answer
                {
                    SubmissionId = submission.SubmissionId,
                    QuestionId   = a.QuestionId,
                    AnswerScalar = fileId.ToString()
                });
            }
            else
            {
                answers.Add(new Answer
                {
                    SubmissionId = submission.SubmissionId,
                    QuestionId   = a.QuestionId,
                    AnswerScalar = a.AnswerScalar?.Trim(),
                    AnswerJson   = a.AnswerJson
                });
            }
        }

        if (answerFiles.Count > 0) db.AnswerFiles.AddRange(answerFiles);
        if (answers.Count > 0)     db.Answers.AddRange(answers);
        if (answerFiles.Count > 0 || answers.Count > 0) await db.SaveChangesAsync();

        return Ok(new { submissionId = submission.SubmissionId });
    }
}

public record SurveyAnswerItem(int QuestionId, string? AnswerScalar, string? AnswerJson);
public record SurveySubmitRequest(List<SurveyAnswerItem> Answers);
file record FilePayload(string FileName, string ContentType, string Data);
