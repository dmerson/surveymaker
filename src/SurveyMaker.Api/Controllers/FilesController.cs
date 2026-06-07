using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SurveyMaker.Infrastructure.Data;

namespace SurveyMaker.Api.Controllers;

[ApiController]
[Route("api/answers/files")]
[Authorize]
public class FilesController(SurveyMakerDbContext db) : ControllerBase
{
    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> GetFile(Guid fileId)
    {
        var file = await db.AnswerFiles
            .Include(f => f.Submission)
                .ThenInclude(s => s.Form)
            .FirstOrDefaultAsync(f => f.FileId == fileId);

        if (file is null) return NotFound();

        var callerEmail = User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        var creatorEmail = file.Submission.Form.FormCreatorEmail?.Trim().ToLowerInvariant();
        var submitterEmail = file.Submission.UserEmail?.Trim().ToLowerInvariant();

        if (callerEmail != creatorEmail && callerEmail != submitterEmail)
            return Forbid();

        return File(file.FileData, file.ContentType, file.FileName);
    }
}
