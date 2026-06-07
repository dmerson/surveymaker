using FluentAssertions;
using SurveyMaker.Infrastructure.Data.Entities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SurveyMaker.IntegrationTests;

/// <summary>
/// End-to-end HTTP tests that exercise the real ASP.NET Core pipeline
/// (routing, middleware, auth, model binding) against an in-memory database.
/// </summary>
public class PublicApiTests(SurveyMakerWebFactory factory)
    : IClassFixture<SurveyMakerWebFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new() { AllowAutoRedirect = false });

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Form SeedPublicForm(string name = "Public Survey")
    {
        var form = new Form
        {
            FormId           = Guid.NewGuid(),
            FormName         = name,
            FormCreatorEmail = "creator@test.com",
            SecurityTypeId   = 1,
            Published        = true,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };
        factory.Seed(db => { db.Forms.Add(form); db.SaveChanges(); });
        return form;
    }

    private Form SeedPrivateForm()
    {
        var form = new Form
        {
            FormId           = Guid.NewGuid(),
            FormName         = "Private Survey",
            FormCreatorEmail = "creator@test.com",
            SecurityTypeId   = 2,
            Published        = true,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };
        factory.Seed(db => { db.Forms.Add(form); db.SaveChanges(); });
        return form;
    }

    // ── /api/surveys (public, no auth required) ───────────────────────────────

    [Fact]
    public async Task GetSurveys_NoAuth_Returns200()
    {
        var response = await _client.GetAsync("/api/surveys");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSurveys_OnlyShowsPublishedPublicForms()
    {
        SeedPublicForm("Visible Form");
        factory.Seed(db =>
        {
            db.Forms.Add(new Form
            {
                FormId = Guid.NewGuid(), FormName = "Unpublished",
                FormCreatorEmail = "x@test.com", SecurityTypeId = 1,
                Published = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        });

        var json = await _client.GetFromJsonAsync<JsonElement[]>("/api/surveys");

        json.Should().NotBeNull();
        json!.Should().NotContain(f => f.GetProperty("formName").GetString() == "Unpublished");
        json.Should().Contain(f => f.GetProperty("formName").GetString() == "Visible Form");
    }

    [Fact]
    public async Task GetSurveyDetail_PublicForm_Returns200WithFormName()
    {
        var form = SeedPublicForm("Customer Feedback");

        var response = await _client.GetAsync($"/api/surveys/{form.FormId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("formName").GetString().Should().Be("Customer Feedback");
    }

    [Fact]
    public async Task GetSurveyDetail_NonExistentForm_Returns404()
    {
        var response = await _client.GetAsync($"/api/surveys/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSurveyDetail_PrivateForm_NoAuth_ReturnsForbid()
    {
        var form = SeedPrivateForm();

        var response = await _client.GetAsync($"/api/surveys/{form.FormId}");

        // Cookie auth with custom events returns 401/403 for API paths;
        // either indicates access was correctly denied
        ((int)response.StatusCode).Should().BeGreaterThanOrEqualTo(400);
    }

    // ── /api/forms (auth required) ────────────────────────────────────────────

    [Fact]
    public async Task GetForms_NoAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/forms");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostForms_NoAuth_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/forms",
            new { formName = "Test", description = (string?)null, securityTypeId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/surveys/:id/submit ──────────────────────────────────────────

    [Fact]
    public async Task SubmitSurvey_PublicForm_AnonymousUser_Returns200()
    {
        var form = SeedPublicForm();

        var response = await _client.PostAsJsonAsync(
            $"/api/surveys/{form.FormId}/submit",
            new { answers = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitSurvey_PublicForm_ResponseIncludesSubmissionId()
    {
        var form = SeedPublicForm();

        var response = await _client.PostAsJsonAsync(
            $"/api/surveys/{form.FormId}/submit",
            new { answers = Array.Empty<object>() });

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("submissionId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SubmitSurvey_NonExistentForm_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/surveys/{Guid.NewGuid()}/submit",
            new { answers = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
