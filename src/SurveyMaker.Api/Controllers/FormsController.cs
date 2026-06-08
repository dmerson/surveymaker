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
        (User.FindFirstValue(ClaimTypes.Email)
        ?? throw new InvalidOperationException("Authenticated user has no email claim."))
        .Trim().ToLowerInvariant();

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
                isMatrix    = s.IsMatrix,
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
        form.SecurityTypeId = request.SecurityTypeId is 1 or 2 or 3 ? request.SecurityTypeId : form.SecurityTypeId;
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

    // ── Answer grid (all responses × all questions) ───────────────────────────

    [HttpGet("{formId:guid}/grid")]
    public async Task<IActionResult> GetAnswerGrid(Guid formId)
    {
        var form = await db.Forms
            .Where(f => f.FormId == formId && f.FormCreatorEmail == UserEmail)
            .Select(f => new { f.FormId, f.FormName })
            .FirstOrDefaultAsync();
        if (form is null) return NotFound();

        var questions = await db.Questions
            .Where(q => q.Section.FormId == formId && q.QuestionTypeId != 0)
            .OrderBy(q => q.Section.Order)
            .ThenBy(q => q.Order)
            .Select(q => new {
                questionId    = q.QuestionId,
                text          = q.Text,
                questionTypeId = q.QuestionTypeId,
                sectionId     = q.SectionId,
                sectionName   = q.Section.SectionName,
                isMatrix      = q.Section.IsMatrix
            })
            .ToListAsync();

        var questionIds = questions.Select(q => q.questionId).ToList();

        var submissions = await db.FormSubmissions
            .Where(s => s.FormId == formId)
            .OrderBy(s => s.SubmittedAt ?? s.StartedAt)
            .Select(s => new
            {
                submissionId = s.SubmissionId,
                userEmail    = s.UserEmail,
                submittedAt  = s.SubmittedAt,
                answers      = s.Answers
                    .Where(a => questionIds.Contains(a.QuestionId))
                    .Select(a => new { a.QuestionId, a.AnswerScalar })
                    .ToList()
            })
            .ToListAsync();

        var rows = submissions.Select(s =>
        {
            var map = s.answers.ToDictionary(a => a.QuestionId, a => a.AnswerScalar);
            return new
            {
                s.submissionId,
                s.userEmail,
                s.submittedAt,
                cells = questionIds.Select(qid =>
                    map.TryGetValue(qid, out var v) ? v : null).ToList()
            };
        });

        return Ok(new { formId = form.FormId, formName = form.FormName, questions, rows });
    }

    // ── Allowed users ─────────────────────────────────────────────────────────

    [HttpGet("{formId:guid}/allowed-users")]
    public async Task<IActionResult> ListAllowedUsers(Guid formId)
    {
        var formExists = await db.Forms
            .AnyAsync(f => f.FormId == formId && f.FormCreatorEmail == UserEmail);
        if (!formExists) return NotFound();

        var users = await db.FormAllowedUsers
            .Where(u => u.FormId == formId)
            .OrderBy(u => u.UserEmail)
            .Select(u => new { u.FormAllowedUserId, u.UserEmail })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("{formId:guid}/allowed-users")]
    public async Task<IActionResult> AddAllowedUser(Guid formId, [FromBody] AddAllowedUserRequest request)
    {
        var email = request.UserEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "Email is required." });

        var formExists = await db.Forms
            .AnyAsync(f => f.FormId == formId && f.FormCreatorEmail == UserEmail);
        if (!formExists) return NotFound();

        var alreadyExists = await db.FormAllowedUsers
            .AnyAsync(u => u.FormId == formId && u.UserEmail == email);
        if (alreadyExists)
            return Conflict(new { error = "This user has already been added." });

        var entry = new FormAllowedUser { FormId = formId, UserEmail = email };
        db.FormAllowedUsers.Add(entry);
        await db.SaveChangesAsync();

        return Ok(new { entry.FormAllowedUserId, entry.UserEmail });
    }

    [HttpDelete("{formId:guid}/allowed-users/{allowedUserId:int}")]
    public async Task<IActionResult> RemoveAllowedUser(Guid formId, int allowedUserId)
    {
        var entry = await db.FormAllowedUsers
            .Include(u => u.Form)
            .FirstOrDefaultAsync(u => u.FormAllowedUserId == allowedUserId
                                   && u.FormId == formId
                                   && u.Form.FormCreatorEmail == UserEmail);
        if (entry is null) return NotFound();

        db.FormAllowedUsers.Remove(entry);
        await db.SaveChangesAsync();
        return Ok();
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

    // ── Rename section ────────────────────────────────────────────────────────

    [HttpPatch("{formId:guid}/sections/{sectionId:int}")]
    public async Task<IActionResult> RenameSection(
        Guid formId, int sectionId, [FromBody] RenameSectionRequest request)
    {
        var section = await db.Sections
            .FirstOrDefaultAsync(s => s.SectionId == sectionId
                                   && s.FormId == formId
                                   && s.Form.FormCreatorEmail == UserEmail);
        if (section is null) return NotFound();

        section.SectionName = request.SectionName?.Trim() ?? string.Empty;

        if (request.Order.HasValue && request.Order.Value > 0 && request.Order.Value != section.Order)
        {
            var conflicting = await db.Sections
                .FirstOrDefaultAsync(s => s.FormId == formId
                                       && s.Order == request.Order.Value
                                       && s.SectionId != sectionId);
            if (conflicting is not null)
                conflicting.Order = section.Order;
            section.Order = request.Order.Value;
        }

        if (request.IsMatrix.HasValue)
            section.IsMatrix = request.IsMatrix.Value;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Delete section ────────────────────────────────────────────────────────

    [HttpDelete("{formId:guid}/sections/{sectionId:int}")]
    public async Task<IActionResult> DeleteSection(Guid formId, int sectionId)
    {
        var sectionCount = await db.Sections
            .CountAsync(s => s.FormId == formId && s.Form.FormCreatorEmail == UserEmail);
        if (sectionCount <= 1)
            return BadRequest(new { error = "Cannot delete the only section." });

        var section = await db.Sections
            .Include(s => s.Questions)
                .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(s => s.SectionId == sectionId
                                   && s.FormId == formId
                                   && s.Form.FormCreatorEmail == UserEmail);
        if (section is null) return NotFound();

        db.Sections.Remove(section);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Add question ──────────────────────────────────────────────────────────

    [HttpPost("{formId:guid}/sections/{sectionId:int}/questions")]
    public async Task<IActionResult> AddQuestion(
        Guid formId, int sectionId, [FromBody] AddQuestionRequest request)
    {
        if (request.QuestionTypeId != 0 && string.IsNullOrWhiteSpace(request.Text))
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
            Text               = request.Text?.Trim() ?? string.Empty,
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
        if (request.QuestionTypeId != 0 && string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Question text is required." });

        var question = await db.Questions
            .Include(q => q.Section)
                .ThenInclude(s => s.Form)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId
                                   && q.SectionId == sectionId
                                   && q.Section.FormId == formId
                                   && q.Section.Form.FormCreatorEmail == UserEmail);
        if (question is null) return NotFound();

        question.Text               = request.Text?.Trim() ?? string.Empty;
        question.QuestionTypeId     = request.QuestionTypeId;
        question.QuestionAttributes = request.QuestionAttributes;

        int newOrder = request.Order > 0 ? request.Order : question.Order;
        if (newOrder != question.Order)
        {
            var conflicting = await db.Questions
                .FirstOrDefaultAsync(q => q.SectionId == sectionId
                                       && q.Order == newOrder
                                       && q.QuestionId != questionId);
            if (conflicting is not null)
                conflicting.Order = question.Order;
            question.Order = newOrder;
        }

        await db.SaveChangesAsync();
        return Ok();
    }

    // ── Reorder question (swap with the question currently at target order) ──

    [HttpPatch("{formId:guid}/sections/{sectionId:int}/questions/{questionId:int}")]
    public async Task<IActionResult> ReorderQuestion(
        Guid formId, int sectionId, int questionId, [FromBody] ReorderQuestionRequest request)
    {
        var question = await db.Questions
            .Include(q => q.Section)
                .ThenInclude(s => s.Form)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId
                                   && q.SectionId == sectionId
                                   && q.Section.FormId == formId
                                   && q.Section.Form.FormCreatorEmail == UserEmail);
        if (question is null) return NotFound();

        int newOrder = request.Order;
        if (newOrder <= 0 || newOrder == question.Order) return NoContent();

        var conflicting = await db.Questions
            .FirstOrDefaultAsync(q => q.SectionId == sectionId
                                   && q.Order == newOrder
                                   && q.QuestionId != questionId);
        if (conflicting is not null)
            conflicting.Order = question.Order;
        question.Order = newOrder;

        await db.SaveChangesAsync();
        return NoContent();
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
public record RenameSectionRequest(string? SectionName, int? Order, bool? IsMatrix);
public record ReorderQuestionRequest(int Order);
public record PatchFormRequest(string? FormName, string? Description, bool RandomizeOrder, int? Quota, bool Published, int SecurityTypeId = 1);
public record AddAllowedUserRequest(string UserEmail);
