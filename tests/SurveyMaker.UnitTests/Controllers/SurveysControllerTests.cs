using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SurveyMaker.Api.Controllers;
using SurveyMaker.Infrastructure.Data;
using SurveyMaker.Infrastructure.Data.Entities;
using System.Text.Json;

namespace SurveyMaker.UnitTests.Controllers;

public class SurveysControllerTests
{
    private readonly SurveyMakerDbContext _db = TestHelpers.CreateDb();

    private SurveysController Ctrl(string? email = null)
    {
        var c = new SurveysController(_db);
        c.ControllerContext = email is not null
            ? TestHelpers.AuthContext(email)
            : TestHelpers.AnonContext();
        return c;
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private Form SeedForm(
        int securityTypeId    = 1,
        bool published        = true,
        string creatorEmail   = "creator@test.com",
        string name           = "Survey")
    {
        var f = new Form
        {
            FormId           = Guid.NewGuid(),
            FormName         = name,
            FormCreatorEmail = creatorEmail,
            SecurityTypeId   = securityTypeId,
            Published        = published,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };
        _db.Forms.Add(f);
        _db.SaveChanges();
        return f;
    }

    private void SeedAllowedUser(Guid formId, string email)
    {
        _db.FormAllowedUsers.Add(new FormAllowedUser { FormId = formId, UserEmail = email });
        _db.SaveChanges();
    }

    private Section SeedSection(Guid formId)
    {
        var s = new Section { FormId = formId, SectionName = "", Order = 1, IsMatrix = false, ShowAsPage = false };
        _db.Sections.Add(s);
        _db.SaveChanges();
        return s;
    }

    private Question SeedQuestion(int sectionId)
    {
        var q = new Question { SectionId = sectionId, Text = "Q?", QuestionTypeId = 1, Order = 1 };
        _db.Questions.Add(q);
        _db.SaveChanges();
        return q;
    }

    private static JsonElement Json(IActionResult result) =>
        JsonSerializer.Deserialize<JsonElement>(
            JsonSerializer.Serialize(result.Should().BeOfType<OkObjectResult>().Subject.Value));

    // ── ListPublic ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListPublic_ReturnsOnlyPublishedPublicForms()
    {
        SeedForm(securityTypeId: 1, published: true,  name: "Visible");
        SeedForm(securityTypeId: 1, published: false, name: "Unpublished");
        SeedForm(securityTypeId: 2, published: true,  name: "Private");
        SeedForm(securityTypeId: 3, published: true,  name: "UrlOnly");

        var json = Json(await Ctrl().ListPublic());

        json.GetArrayLength().Should().Be(1);
        json[0].GetProperty("formName").GetString().Should().Be("Visible");
    }

    [Fact]
    public async Task ListPublic_ReturnsQuestionCount()
    {
        var form    = SeedForm(securityTypeId: 1);
        var section = SeedSection(form.FormId);
        SeedQuestion(section.SectionId);
        SeedQuestion(section.SectionId);

        var json = Json(await Ctrl().ListPublic());

        json[0].GetProperty("questionCount").GetInt32().Should().Be(2);
    }

    // ── GetSurvey ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSurvey_PublicForm_AnyoneCanAccess()
    {
        var form = SeedForm(securityTypeId: 1);

        var result = await Ctrl().GetSurvey(form.FormId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSurvey_UrlAllowedForm_AnonymousCanAccess()
    {
        var form = SeedForm(securityTypeId: 3);

        var result = await Ctrl(email: null).GetSurvey(form.FormId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSurvey_PrivateForm_AnonymousReturnsForbid()
    {
        var form = SeedForm(securityTypeId: 2);

        var result = await Ctrl(email: null).GetSurvey(form.FormId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetSurvey_PrivateForm_AuthenticatedButNotAllowed_ReturnsForbid()
    {
        var form = SeedForm(securityTypeId: 2, creatorEmail: "creator@test.com");

        var result = await Ctrl("stranger@test.com").GetSurvey(form.FormId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetSurvey_PrivateForm_UserInAllowedList_ReturnsOk()
    {
        var form = SeedForm(securityTypeId: 2, creatorEmail: "creator@test.com");
        SeedAllowedUser(form.FormId, "allowed@test.com");

        var result = await Ctrl("allowed@test.com").GetSurvey(form.FormId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSurvey_PrivateForm_CreatorCanAccess()
    {
        var form = SeedForm(securityTypeId: 2, creatorEmail: "creator@test.com");

        var result = await Ctrl("creator@test.com").GetSurvey(form.FormId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetSurvey_UnpublishedForm_ReturnsNotFound()
    {
        var form = SeedForm(securityTypeId: 1, published: false);

        var result = await Ctrl().GetSurvey(form.FormId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetSurvey_ResponseIncludesSectionsAndQuestions()
    {
        var form    = SeedForm(securityTypeId: 1);
        var section = SeedSection(form.FormId);
        SeedQuestion(section.SectionId);

        var json = Json(await Ctrl().GetSurvey(form.FormId));

        json.GetProperty("formName").GetString().Should().Be("Survey");
        json.GetProperty("sections").GetArrayLength().Should().Be(1);
        json.GetProperty("sections")[0].GetProperty("questions").GetArrayLength().Should().Be(1);
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_PublicForm_AnonymousUser_CreatesSubmissionWithNullEmail()
    {
        var form = SeedForm(securityTypeId: 1);

        var result = await Ctrl(email: null).Submit(form.FormId, new SurveySubmitRequest([]));

        result.Should().BeOfType<OkObjectResult>();
        _db.FormSubmissions.Should().HaveCount(1);
        _db.FormSubmissions.Single().UserEmail.Should().BeNull();
    }

    [Fact]
    public async Task Submit_PublicForm_AuthenticatedUser_RecordsEmail()
    {
        var form = SeedForm(securityTypeId: 1);

        await Ctrl("user@test.com").Submit(form.FormId, new SurveySubmitRequest([]));

        _db.FormSubmissions.Single().UserEmail.Should().Be("user@test.com");
    }

    [Fact]
    public async Task Submit_PrivateForm_NotAllowed_ReturnsForbidAndSavesNothing()
    {
        var form = SeedForm(securityTypeId: 2, creatorEmail: "creator@test.com");

        var result = await Ctrl("stranger@test.com").Submit(form.FormId, new SurveySubmitRequest([]));

        result.Should().BeOfType<ForbidResult>();
        _db.FormSubmissions.Should().BeEmpty();
    }

    [Fact]
    public async Task Submit_SavesAnswers()
    {
        var form    = SeedForm(securityTypeId: 1);
        var section = SeedSection(form.FormId);
        var q       = SeedQuestion(section.SectionId);

        await Ctrl().Submit(form.FormId, new SurveySubmitRequest(
        [
            new SurveyAnswerItem(q.QuestionId, "my answer", null)
        ]));

        _db.Answers.Should().HaveCount(1);
        _db.Answers.Single().AnswerScalar.Should().Be("my answer");
    }

    [Fact]
    public async Task Submit_EmptyAnswerScalar_IsNotSaved()
    {
        var form    = SeedForm(securityTypeId: 1);
        var section = SeedSection(form.FormId);
        var q       = SeedQuestion(section.SectionId);

        await Ctrl().Submit(form.FormId, new SurveySubmitRequest(
        [
            new SurveyAnswerItem(q.QuestionId, "   ", null)
        ]));

        _db.FormSubmissions.Should().HaveCount(1);
        _db.Answers.Should().BeEmpty(); // whitespace-only answers are skipped
    }

    [Fact]
    public async Task Submit_UnknownForm_ReturnsNotFound()
    {
        var result = await Ctrl().Submit(Guid.NewGuid(), new SurveySubmitRequest([]));

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Submit_ReturnsSubmissionId()
    {
        var form = SeedForm(securityTypeId: 1);

        var json = Json(await Ctrl().Submit(form.FormId, new SurveySubmitRequest([])));

        json.GetProperty("submissionId").GetString().Should().NotBeNullOrEmpty();
    }
}
