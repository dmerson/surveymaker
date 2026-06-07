using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SurveyMaker.Infrastructure.Data;

namespace SurveyMaker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestionTypesController(SurveyMakerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var types = await db.QuestionTypes
            .OrderBy(qt => qt.QuestionTypeId)
            .Select(qt => new { qt.QuestionTypeId, qt.QuestionTypeName })
            .ToListAsync();

        return Ok(types);
    }
}
