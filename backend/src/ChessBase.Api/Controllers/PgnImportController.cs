using ChessBase.Application.Abstractions;
using ChessBase.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ChessBase.Api.Controllers;

[ApiController]
[Route("api/pgn")]
public class PgnImportController(IPgnImportService pgnImportService) : ControllerBase
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
}
