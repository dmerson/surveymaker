using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SurveyMaker.Api.Controllers;
using SurveyMaker.Infrastructure.Data;
using SurveyMaker.Infrastructure.Data.Entities;
using System.Text.Json;

namespace SurveyMaker.UnitTests.Controllers;

public class FormsControllerTests
{
    private readonly SurveyMakerDbContext _db = TestHelpers.CreateDb();

    private const string Owner = "owner@test.com";
    private const string Other = "other@test.com";

    private FormsController Ctrl(string email = Owner)
    {
        var c = new FormsController(_db);
        c.ControllerContext = TestHelpers.AuthContext(email);
        return c;
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private Form SeedForm(string email = Owner, string name = "Test Form")
    {
        var f = new Form
        {
            FormId           = Guid.NewGuid(),
            FormName         = name,
            FormCreatorEmail = email,
            SecurityTypeId   = 1,
            Published        = false,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };
        _db.Forms.Add(f);
        _db.SaveChanges();
        return f;
    }

    private Section SeedSection(Guid formId, string name = "Section 1")
    {
        var s = new Section { FormId = formId, SectionName = name, Order = 1, IsMatrix = false, ShowAsPage = false };
        _db.Sections.Add(s);
        _db.SaveChanges();
        return s;
    }

    private Question SeedQuestion(int sectionId, int order = 1)
    {
        // QuestionTypeId 1 = "Text" — seeded by EnsureCreated via HasData
        var q = new Question { SectionId = sectionId, Text = "Test question?", QuestionTypeId = 1, Order = order };
        _db.Questions.Add(q);
        _db.SaveChanges();
        return q;
    }

    private FormSubmission SeedSubmission(Guid formId, string? email = null)
    {
        var s = new FormSubmission
        {
            SubmissionId = Guid.NewGuid(),
            FormId       = formId,
            UserEmail    = email,
            StartedAt    = DateTime.UtcNow,
            SubmittedAt  = DateTime.UtcNow,
            IsComplete   = true
        };
        _db.FormSubmissions.Add(s);
        _db.SaveChanges();
        return s;
    }

    private Answer SeedAnswer(Guid submissionId, int questionId, string value = "answer")
    {
        var a = new Answer { SubmissionId = submissionId, QuestionId = questionId, AnswerScalar = value };
        _db.Answers.Add(a);
        _db.SaveChanges();
        return a;
    }

    private static JsonElement Json(IActionResult result) =>
        JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(result.Should().BeOfType<OkObjectResult>().Subject.Value));

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsOnlyCurrentUserForms()
    {
        SeedForm(Owner, "Mine 1");
        SeedForm(Owner, "Mine 2");
        SeedForm(Other, "Not Mine");

        var json = Json(await Ctrl(Owner).List());

        json.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task List_IncludesResponseCount()
    {
        var form = SeedForm();
        SeedSubmission(form.FormId);
        SeedSubmission(form.FormId);

        var json = Json(await Ctrl().List());

        json[0].GetProperty("responseCount").GetInt32().Should().Be(2);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_CreatesFormAndDefaultSection()
    {
        var result = await Ctrl().Create(new CreateFormRequest("New Survey", "desc", 1));

        result.Should().BeOfType<OkObjectResult>();
        _db.Forms.Should().HaveCount(1);
        _db.Sections.Should().HaveCount(1);
        _db.Forms.Single().Published.Should().BeFalse();
        _db.Forms.Single().FormCreatorEmail.Should().Be(Owner);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        var result = await Ctrl().Create(new CreateFormRequest("  ", null, 1));

        result.Should().BeOfType<BadRequestObjectResult>();
        _db.Forms.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_InvalidSecurityTypeId_DefaultsToPublic()
    {
        await Ctrl().Create(new CreateFormRequest("Survey", null, 99));

        _db.Forms.Single().SecurityTypeId.Should().Be(1);
    }

    // ── GetDetail ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDetail_OtherUserForm_ReturnsNotFound()
    {
        var form = SeedForm(Other);

        var result = await Ctrl(Owner).GetDetail(form.FormId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDetail_OwnForm_ReturnsFormWithSections()
    {
        var form = SeedForm(Owner, "My Survey");
        SeedSection(form.FormId, "Section A");

        var json = Json(await Ctrl().GetDetail(form.FormId));

        json.GetProperty("formName").GetString().Should().Be("My Survey");
        json.GetProperty("sections").GetArrayLength().Should().Be(1);
    }

    // ── PatchSettings ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchSettings_UpdatesAllFields()
    {
        var form = SeedForm();

        await Ctrl().PatchSettings(form.FormId,
            new PatchFormRequest("Renamed", "New desc", true, 50, true));

        var updated = _db.Forms.Single();
        updated.FormName.Should().Be("Renamed");
        updated.Description.Should().Be("New desc");
        updated.RandomizeOrder.Should().BeTrue();
        updated.Quota.Should().Be(50);
        updated.Published.Should().BeTrue();
    }

    [Fact]
    public async Task PatchSettings_ZeroQuota_SetsToNull()
    {
        var form = SeedForm();

        await Ctrl().PatchSettings(form.FormId,
            new PatchFormRequest("Form", null, false, 0, false));

        _db.Forms.Single().Quota.Should().BeNull();
    }

    [Fact]
    public async Task PatchSettings_OtherUserForm_ReturnsNotFound()
    {
        var form = SeedForm(Other);

        var result = await Ctrl(Owner).PatchSettings(form.FormId,
            new PatchFormRequest("X", null, false, null, false));

        result.Should().BeOfType<NotFoundResult>();
    }

    // ── DeleteForm ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteForm_RemovesForm()
    {
        var form = SeedForm();

        var result = await Ctrl().DeleteForm(form.FormId);

        result.Should().BeOfType<OkResult>();
        _db.Forms.Should().BeEmpty();
    }

    // ── AddSection ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddSection_ValidRequest_CreatesAndReturnsSection()
    {
        var form = SeedForm();

        var result = await Ctrl().AddSection(form.FormId, new AddSectionRequest("Part 2", 2));

        var json = Json(result);
        json.GetProperty("sectionName").GetString().Should().Be("Part 2");
        json.GetProperty("order").GetInt32().Should().Be(2);
        _db.Sections.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddSection_OtherUserForm_ReturnsNotFound()
    {
        var form = SeedForm(Other);

        var result = await Ctrl(Owner).AddSection(form.FormId, new AddSectionRequest("X", 1));

        result.Should().BeOfType<NotFoundResult>();
    }

    // ── AddQuestion ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AddQuestion_EmptyText_ReturnsBadRequest()
    {
        var form    = SeedForm();
        var section = SeedSection(form.FormId);

        var result = await Ctrl().AddQuestion(form.FormId, section.SectionId,
            new AddQuestionRequest(1, "  ", 0, null));

        result.Should().BeOfType<BadRequestObjectResult>();
        _db.Questions.Should().BeEmpty();
    }

    [Fact]
    public async Task AddQuestion_NoOrderProvided_AutoIncrementsFromExisting()
    {
        var form    = SeedForm();
        var section = SeedSection(form.FormId);
        SeedQuestion(section.SectionId, order: 1);

        await Ctrl().AddQuestion(form.FormId, section.SectionId,
            new AddQuestionRequest(1, "Second question?", 0, null));

        _db.Questions.Max(q => q.Order).Should().Be(2);
    }

    [Fact]
    public async Task AddQuestion_FirstQuestion_GetsOrderOne()
    {
        var form    = SeedForm();
        var section = SeedSection(form.FormId);

        var json = Json(await Ctrl().AddQuestion(form.FormId, section.SectionId,
            new AddQuestionRequest(1, "First?", 0, null)));

        json.GetProperty("order").GetInt32().Should().Be(1);
    }

    // ── UpdateQuestion ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateQuestion_OtherUserForm_ReturnsNotFound()
    {
        var form     = SeedForm(Other);
        var section  = SeedSection(form.FormId);
        var question = SeedQuestion(section.SectionId);

        var result = await Ctrl(Owner).UpdateQuestion(form.FormId, section.SectionId, question.QuestionId,
            new AddQuestionRequest(1, "Updated?", 1, null));

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateQuestion_UpdatesTextAndType()
    {
        var form     = SeedForm();
        var section  = SeedSection(form.FormId);
        var question = SeedQuestion(section.SectionId);

        await Ctrl().UpdateQuestion(form.FormId, section.SectionId, question.QuestionId,
            new AddQuestionRequest(2, "Updated text?", 1, null));

        var updated = _db.Questions.Single();
        updated.Text.Should().Be("Updated text?");
        updated.QuestionTypeId.Should().Be(2);
    }

    // ── RemoveQuestion ────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveQuestion_AlsoDeletesAnswers()
    {
        var form     = SeedForm();
        var section  = SeedSection(form.FormId);
        var question = SeedQuestion(section.SectionId);
        var sub      = SeedSubmission(form.FormId);
        SeedAnswer(sub.SubmissionId, question.QuestionId);

        var result = await Ctrl().RemoveQuestion(form.FormId, section.SectionId, question.QuestionId);

        result.Should().BeOfType<OkResult>();
        _db.Questions.Should().BeEmpty();
        _db.Answers.Should().BeEmpty();
    }

    // ── ListSubmissions ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListSubmissions_OtherUserForm_ReturnsNotFound()
    {
        var form = SeedForm(Other);

        var result = await Ctrl(Owner).ListSubmissions(form.FormId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ListSubmissions_ReturnsAllSubmissionsWithFormName()
    {
        var form = SeedForm(Owner, "Feedback Survey");
        SeedSubmission(form.FormId, "a@test.com");
        SeedSubmission(form.FormId, "b@test.com");

        var json = Json(await Ctrl().ListSubmissions(form.FormId));

        json.GetProperty("formName").GetString().Should().Be("Feedback Survey");
        json.GetProperty("submissions").GetArrayLength().Should().Be(2);
    }

    // ── GetSubmission ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSubmission_OtherUserForm_ReturnsNotFound()
    {
        var form = SeedForm(Other);
        var sub  = SeedSubmission(form.FormId);

        var result = await Ctrl(Owner).GetSubmission(form.FormId, sub.SubmissionId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSubmission_BakesAnswersIntoQuestions()
    {
        var form     = SeedForm();
        var section  = SeedSection(form.FormId);
        var question = SeedQuestion(section.SectionId);
        var sub      = SeedSubmission(form.FormId, "resp@test.com");
        SeedAnswer(sub.SubmissionId, question.QuestionId, "42");

        var json = Json(await Ctrl().GetSubmission(form.FormId, sub.SubmissionId));

        json.GetProperty("userEmail").GetString().Should().Be("resp@test.com");
        var q = json.GetProperty("sections")[0].GetProperty("questions")[0];
        q.GetProperty("answerScalar").GetString().Should().Be("42");
    }

    [Fact]
    public async Task GetSubmission_UnansweredQuestion_HasNullAnswer()
    {
        var form     = SeedForm();
        var section  = SeedSection(form.FormId);
        SeedQuestion(section.SectionId);
        var sub = SeedSubmission(form.FormId);
        // no answer seeded

        var json = Json(await Ctrl().GetSubmission(form.FormId, sub.SubmissionId));

        var q = json.GetProperty("sections")[0].GetProperty("questions")[0];
        q.GetProperty("answerScalar").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
