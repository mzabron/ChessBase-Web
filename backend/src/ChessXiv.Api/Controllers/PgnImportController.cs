using ChessXiv.Application.Abstractions;
using ChessXiv.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChessXiv.Api.Controllers;

[ApiController]
[Route("api/pgn")]
public class PgnImportController(
    IPgnImportService pgnImportService,
    IDraftImportService draftImportService,
    IDraftPromotionService draftPromotionService) : ControllerBase
{
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] PgnImportRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Pgn))
        {
            return BadRequest("PGN content is required.");
        }

        using var reader = new StringReader(request.Pgn);
        var result = await pgnImportService.ImportAsync(reader, cancellationToken: cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("drafts/import")]
    public async Task<IActionResult> ImportDraft([FromBody] DraftImportRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Pgn))
        {
            return BadRequest("PGN content is required.");
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        using var reader = new StringReader(request.Pgn);
        var result = await draftImportService.ImportAsync(
            reader,
            userId,
            request.ImportSessionId,
            cancellationToken: cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("drafts/{importSessionId:guid}/promote")]
    public async Task<IActionResult> PromoteDraft(
        Guid importSessionId,
        [FromBody] DraftPromotionRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.UserDatabaseId == Guid.Empty)
        {
            return BadRequest("User database id is required.");
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await draftPromotionService.PromoteAsync(
            userId,
            importSessionId,
            request.UserDatabaseId,
            request.DuplicateHandling,
            cancellationToken);

        return Ok(result);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }
}
