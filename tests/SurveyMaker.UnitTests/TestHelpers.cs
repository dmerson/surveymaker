using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SurveyMaker.Infrastructure.Data;
using System.Security.Claims;

namespace SurveyMaker.UnitTests;

internal static class TestHelpers
{
    /// <summary>
    /// Creates a fresh EF Core InMemory database per test.
    /// Each call uses a unique database name so test methods are fully isolated.
    /// EnsureCreated() applies HasData seeds (QuestionTypes, SecurityTypes).
    /// InMemory does not support ExecuteDeleteAsync — use RemoveRange instead.
    ///</summary>
    internal static SurveyMakerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<SurveyMakerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new SurveyMakerDbContext(opts);
        db.Database.EnsureCreated(); // applies HasData seeds (QuestionTypes, SecurityTypes)
        return db;
    }

    internal static ControllerContext AuthContext(string email) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.Email, email)], "Test"))
        }
    };

    internal static ControllerContext AnonContext() => new()
    {
        HttpContext = new DefaultHttpContext()
    };
}
