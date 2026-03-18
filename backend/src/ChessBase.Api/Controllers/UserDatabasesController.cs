using System.Security.Claims;
using ChessBase.Application.Contracts;
using ChessBase.Domain.Entities;
using ChessBase.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChessBase.Api.Controllers;

[ApiController]
[Route("api/user-databases")]
public class UserDatabasesController(ChessBaseDbContext dbContext) : ControllerBase
{
    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var items = await dbContext.UserDatabases
            .AsNoTracking()
            .Where(d => d.OwnerUserId == userId)
            .OrderBy(d => d.Name)
            .Select(d => new UserDatabaseDto(
                d.Id,
                d.Name,
                d.IsPublic,
                d.OwnerUserId,
                d.UserDatabaseGames.Count,
                d.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var dto = await dbContext.UserDatabases
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new UserDatabaseDto(
                d.Id,
                d.Name,
                d.IsPublic,
                d.OwnerUserId,
                d.UserDatabaseGames.Count,
                d.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
        {
            return NotFound();
        }

        if (!dto.IsPublic && dto.OwnerUserId != userId)
        {
            return Forbid();
        }

        return Ok(dto);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDatabaseRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Database name is required.");
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var normalizedName = request.Name.Trim();
        var exists = await dbContext.UserDatabases
            .AnyAsync(d => d.OwnerUserId == userId && d.Name == normalizedName, cancellationToken);

        if (exists)
        {
            return Conflict("A database with this name already exists for this user.");
        }

        var entity = new UserDatabase
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            IsPublic = request.IsPublic,
            OwnerUserId = userId,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.UserDatabases.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new UserDatabaseDto(entity.Id, entity.Name, entity.IsPublic, entity.OwnerUserId, 0, entity.CreatedAtUtc);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDatabaseRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Database name is required.");
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var entity = await dbContext.UserDatabases.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (entity.OwnerUserId != userId)
        {
            return Forbid();
        }

        var normalizedName = request.Name.Trim();
        var duplicate = await dbContext.UserDatabases
            .AnyAsync(d => d.OwnerUserId == userId && d.Name == normalizedName && d.Id != id, cancellationToken);

        if (duplicate)
        {
            return Conflict("A database with this name already exists for this user.");
        }

        entity.Name = normalizedName;
        entity.IsPublic = request.IsPublic;

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var entity = await dbContext.UserDatabases.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (entity.OwnerUserId != userId)
        {
            return Forbid();
        }

        dbContext.UserDatabases.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/games")]
    public async Task<IActionResult> AddGames(Guid id, [FromBody] AddGamesToDatabaseRequest request, CancellationToken cancellationToken)
    {
        if (request?.GameIds is null || request.GameIds.Count == 0)
        {
            return BadRequest("At least one game id is required.");
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var dbEntity = await dbContext.UserDatabases.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dbEntity is null)
        {
            return NotFound();
        }

        if (dbEntity.OwnerUserId != userId)
        {
            return Forbid();
        }

        var distinctGameIds = request.GameIds.Where(g => g != Guid.Empty).Distinct().ToArray();
        if (distinctGameIds.Length == 0)
        {
            return BadRequest("Provided game ids are invalid.");
        }

        var existingGames = await dbContext.Games
            .Where(g => distinctGameIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Date, g.Year, g.Event, g.Round, g.Site })
            .ToListAsync(cancellationToken);

        var existingGameIds = existingGames.Select(g => g.Id).ToArray();

        var missing = distinctGameIds.Except(existingGameIds).ToArray();
        if (missing.Length > 0)
        {
            return NotFound(new { MissingGameIds = missing });
        }

        var alreadyLinked = await dbContext.UserDatabaseGames
            .Where(x => x.UserDatabaseId == id && distinctGameIds.Contains(x.GameId))
            .Select(x => x.GameId)
            .ToListAsync(cancellationToken);

        var existingGameMap = existingGames.ToDictionary(g => g.Id);

        var toInsert = distinctGameIds.Except(alreadyLinked)
            .Select(gameId =>
            {
                var game = existingGameMap[gameId];
                return new UserDatabaseGame
                {
                    UserDatabaseId = id,
                    GameId = gameId,
                    AddedAtUtc = DateTime.UtcNow,
                    Date = game.Date,
                    Year = game.Year,
                    Event = game.Event,
                    Round = game.Round,
                    Site = game.Site
                };
            })
            .ToArray();

        if (toInsert.Length > 0)
        {
            dbContext.UserDatabaseGames.AddRange(toInsert);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new
        {
            AddedCount = toInsert.Length,
            SkippedCount = alreadyLinked.Count
        });
    }

    [Authorize]
    [HttpDelete("{id:guid}/games/{gameId:guid}")]
    public async Task<IActionResult> RemoveGame(Guid id, Guid gameId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var dbEntity = await dbContext.UserDatabases.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dbEntity is null)
        {
            return NotFound();
        }

        if (dbEntity.OwnerUserId != userId)
        {
            return Forbid();
        }

        var link = await dbContext.UserDatabaseGames
            .FirstOrDefaultAsync(x => x.UserDatabaseId == id && x.GameId == gameId, cancellationToken);

        if (link is null)
        {
            return NotFound();
        }

        dbContext.UserDatabaseGames.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }
}
