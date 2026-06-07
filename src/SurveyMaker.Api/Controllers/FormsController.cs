using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SurveyMaker.Infrastructure.Data;
using SurveyMaker.Infrastructure.Data.Entities;

namespace SurveyMaker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FormsController(SurveyMakerDbContext db) : ControllerBase
{
    private string UserEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new InvalidOperationException("Authenticated user has no email claim.");

    // ── List ─────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var forms = await db.Forms
            .Where(f => f.FormCreatorEmail == UserEmail)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                formId        = f.FormId,
                formName      = f.FormName,
                description   = f.Description,
                securityTypeId = f.SecurityTypeId,
                published     = f.Published,
                questionCount  = f.Sections.SelectMany(s => s.Questions).Count(),
                responseCount  = f.Submissions.Count,
                createdAt      = f.CreatedAt
            })
            .ToListAsync();

        return Ok(forms);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFormRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FormName))
            return BadRequest(new { error = "Form name is required." });

        var secTypeId = request.SecurityTypeId is >= 1 and <= 3 ? request.SecurityTypeId : 1;

        var form = new Form
        {
            FormId           = Guid.NewGuid(),
            FormName         = request.FormName.Trim(),
            FormCreatorEmail = UserEmail,
            Description      = request.Description?.Trim(),
            SecurityTypeId   = secTypeId,
            RandomizeOrder   = false,
            Published        = false,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };

        var defaultSection = new Section
        {
            FormId      = form.FormId,
            SectionName = string.Empty,
            IsMatrix    = false,
            Order       = 1,
            ShowAsPage  = false
        };

        db.Forms.Add(form);
        db.Sections.Add(defaultSection);
        await db.SaveChangesAsync();

        return Ok(new { formId = form.FormId });
    }

    // ── Detail ────────────────────────────────────────────────────────────────

    [HttpGet("{formId:guid}")]
    public async Task<IActionResult> GetDetail(Guid formId)
    {
        var form = await db.Forms
            .Include(f => f.Sections.OrderBy(s => s.Order))
                .ThenInclude(s => s.Questions.OrderBy(q => q.Order))
                    .ThenInclude(q => q.QuestionType)
            .FirstOrDefaultAsync(f => f.FormId == formId && f.FormCreatorEmail == UserEmail);

        if (form is null) return NotFound();

        return Ok(new
        {
            formId         = form.FormId,
            formName       = form.FormName,
            description    = form.Description,
            securityTypeId = form.SecurityTypeId,
            randomizeOrder = form.RandomizeOrder,
            quota          = form.Quota,
            published      = form.Published,
            sections       = form.Sections.Select(s => new
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

    // ── Patch settings ────────────────────────────────────────────────────────

    [HttpPatch("{formId:guid}")]
    public async Task<IActionResult> PatchSettings(Guid formId, [FromBody] PatchFormRequest request)
    {
        var form = await db.Forms
            .FirstOrDefaultAsync(f => f.FormId == formId && f.FormCreatorEmail == UserEmail);
        if (form is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.FormName))
            form.FormName = request.FormName.Trim();
        form.Description    = request.Description?.Trim();
        form.RandomizeOrder = request.RandomizeOrder;
        form.Quota          = request.Quota > 0 ? request.Quota : null;
        form.Published      = request.Published;
        form.UpdatedAt      = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok();
    }

    // ── Delete form ───────────────────────────────────────────────────────────

    [HttpDelete("{formId:guid}")]
    public async Task<IActionResult> DeleteForm(Guid formId)
    {
        var form = await db.Forms
            .FirstOrDefaultAsync(f => f.FormId == formId && f.FormCreatorEmail == UserEmail);
        if (form is null) return NotFound();

        // Remove answers first — Questions→Answers FK is NO ACTION so won't cascade
        var submissionIds = await db.FormSubmissions
            .Where(s => s.FormId == formId)
            .Select(s => s.SubmissionId)
            .ToListAsync();

        if (submissionIds.Count > 0)
            await db.Answers.Where(a => submissionIds.Contains(a.SubmissionId)).ExecuteDeleteAsync();

        db.Forms.Remove(form);
        await db.SaveChangesAsync();
        return Ok();
    }

    // ── List submissions ──────────────────────────────────────────────────────

    [HttpGet("{formId:guid}/submissions")]
    public async Task<IActionResult> ListSubmissions(Guid formId)
    {
        var form = await db.Forms
            .Where(f => f.FormId == formId && f.FormCreatorEmail == UserEmail)
            .Select(f => new { f.FormId, f.FormName })
            .FirstOrDefaultAsync();
        if (form is null) return NotFound();

        var submissions = await db.FormSubmissions
            .Where(s => s.FormId == formId)
            .OrderByDescending(s => s.SubmittedAt ?? s.StartedAt)
            .Select(s => new
            {
                submissionId = s.SubmissionId,
                userEmail    = s.UserEmail,
                submittedAt  = s.SubmittedAt,
                isComplete   = s.IsComplete
            })
            .ToListAsync();

        return Ok(new { formId = form.FormId, formName = form.FormName, submissions });
    }

    // ── Get submission detail ─────────────────────────────────────────────────

    [HttpGet("{formId:guid}/submissions/{submissionId:guid}")]
    public async Task<IActionResult> GetSubmission(Guid formId, Guid submissionId)
    {
        var form = await db.Forms
            .Include(f => f.Sections.OrderBy(s => s.Order))
                .ThenInclude(s => s.Questions.OrderBy(q => q.Order))
                    .ThenInclude(q => q.QuestionType)
            .FirstOrDefaultAsync(f => f.FormId == formId && f.FormCreatorEmail == UserEmail);
        if (form is null) return NotFound();

        var submission = await db.FormSubmissions
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && s.FormId == formId);
        if (submission is null) return NotFound();

        var answerMap = submission.Answers.ToDictionary(a => a.QuestionId);

        return Ok(new
        {
            submissionId = submission.SubmissionId,
            formId       = form.FormId,
            formName     = form.FormName,
            description  = form.Description,
            userEmail    = submission.UserEmail,
            submittedAt  = submission.SubmittedAt,
            sections     = form.Sections.Select(s => new
            {
                sectionId   = s.SectionId,
                sectionName = s.SectionName,
                order       = s.Order,
                questions   = s.Questions.Select(q =>
                {
                    answerMap.TryGetValue(q.QuestionId, out var a);
                    return new
                    {
                        questionId         = q.QuestionId,
                        order              = q.Order,
                        text               = q.Text,
                        questionTypeId     = q.QuestionTypeId,
                        questionTypeName   = q.QuestionType.QuestionTypeName,
                        questionAttributes = q.QuestionAttributes,
                        answerScalar       = a?.AnswerScalar,
                        answerJson         = a?.AnswerJson
                    };
                })
            })
        });
    }

    // ── Add section ───────────────────────────────────────────────────────────

    [HttpPost("{formId:guid}/sections")]
    public async Task<IActionResult> AddSection(Guid formId, [FromBody] AddSectionRequest request)
    {
        var formExists = await db.Forms
            .AnyAsync(f => f.FormId == formId && f.FormCreatorEmail == UserEmail);
        if (!formExists) return NotFound();

        var section = new Section
        {
            FormId      = formId,
            SectionName = request.SectionName?.Trim() ?? string.Empty,
            IsMatrix    = false,
            Order       = request.Order > 0 ? request.Order : 999,
            ShowAsPage  = false
        };

        db.Sections.Add(section);
        await db.SaveChangesAsync();

        return Ok(new
        {
            sectionId   = section.SectionId,
            sectionName = section.SectionName,
            order       = section.Order
        });
    }

    // ── Add question ──────────────────────────────────────────────────────────

    [HttpPost("{formId:guid}/sections/{sectionId:int}/questions")]
    public async Task<IActionResult> AddQuestion(
        Guid formId, int sectionId, [FromBody] AddQuestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Question text is required." });

        var sectionExists = await db.Sections
            .AnyAsync(s => s.SectionId == sectionId
                        && s.FormId == formId
                        && s.Form.FormCreatorEmail == UserEmail);
        if (!sectionExists) return NotFound();

        var maxOrder = await db.Questions
            .Where(q => q.SectionId == sectionId)
            .Select(q => (int?)q.Order)
            .MaxAsync() ?? 0;

        var question = new Question
        {
            SectionId          = sectionId,
            Order              = request.Order > 0 ? request.Order : maxOrder + 1,
            Text               = request.Text.Trim(),
            QuestionTypeId     = request.QuestionTypeId,
            QuestionAttributes = request.QuestionAttributes
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync();

        return Ok(new { questionId = question.QuestionId, order = question.Order });
    }

    // ── Update question ───────────────────────────────────────────────────────

    [HttpPut("{formId:guid}/sections/{sectionId:int}/questions/{questionId:int}")]
    public async Task<IActionResult> UpdateQuestion(
        Guid formId, int sectionId, int questionId, [FromBody] AddQuestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Question text is required." });

        var question = await db.Questions
            .Include(q => q.Section)
                .ThenInclude(s => s.Form)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId
                                   && q.SectionId == sectionId
                                   && q.Section.FormId == formId
                                   && q.Section.Form.FormCreatorEmail == UserEmail);
        if (question is null) return NotFound();

        question.Text               = request.Text.Trim();
        question.QuestionTypeId     = request.QuestionTypeId;
        question.Order              = request.Order > 0 ? request.Order : question.Order;
        question.QuestionAttributes = request.QuestionAttributes;

        await db.SaveChangesAsync();
        return Ok();
    }

    // ── Remove question ───────────────────────────────────────────────────────

    [HttpDelete("{formId:guid}/sections/{sectionId:int}/questions/{questionId:int}")]
    public async Task<IActionResult> RemoveQuestion(Guid formId, int sectionId, int questionId)
    {
        var question = await db.Questions
            .Include(q => q.Section)
                .ThenInclude(s => s.Form)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId
                                   && q.SectionId == sectionId
                                   && q.Section.FormId == formId
                                   && q.Section.Form.FormCreatorEmail == UserEmail);
        if (question is null) return NotFound();

        var answers = await db.Answers.Where(a => a.QuestionId == questionId).ToListAsync();
        db.Answers.RemoveRange(answers);
        db.Questions.Remove(question);
        await db.SaveChangesAsync();
        return Ok();
    }
}

public record CreateFormRequest(string FormName, string? Description, int SecurityTypeId = 1);
public record AddQuestionRequest(int QuestionTypeId, string Text, int Order, string? QuestionAttributes);
public record AddSectionRequest(string? SectionName, int Order);
public record PatchFormRequest(string? FormName, string? Description, bool RandomizeOrder, int? Quota, bool Published);
