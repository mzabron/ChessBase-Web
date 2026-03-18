using ChessBase.Application.Abstractions.Repositories;
using ChessBase.Domain.Entities;
using ChessBase.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChessBase.Infrastructure.Repositories;

public sealed class DraftPromotionRepository(ChessBaseDbContext dbContext) : IDraftPromotionRepository
{
    public Task<StagingImportSession?> GetSessionAsync(Guid importSessionId, string ownerUserId, CancellationToken cancellationToken = default)
    {
        return dbContext.StagingImportSessions
            .FirstOrDefaultAsync(s => s.Id == importSessionId && s.OwnerUserId == ownerUserId, cancellationToken);
    }

    public Task<UserDatabase?> GetUserDatabaseAsync(Guid userDatabaseId, CancellationToken cancellationToken = default)
    {
        return dbContext.UserDatabases
            .FirstOrDefaultAsync(d => d.Id == userDatabaseId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<StagingGame>> GetStagingGamesPageAsync(
        Guid importSessionId,
        string ownerUserId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.StagingGames
            .AsNoTracking()
            .Where(g => g.ImportSessionId == importSessionId && g.OwnerUserId == ownerUserId)
            .OrderBy(g => g.Id)
            .Take(take)
            .Include(g => g.Moves)
            .Include(g => g.Positions)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserDatabaseGame>> GetExistingLinksByGameHashesAsync(
        Guid userDatabaseId,
        IReadOnlyCollection<string> gameHashes,
        CancellationToken cancellationToken = default)
    {
        if (gameHashes.Count == 0)
        {
            return Array.Empty<UserDatabaseGame>();
        }

        return await dbContext.UserDatabaseGames
            .Where(link => link.UserDatabaseId == userDatabaseId && gameHashes.Contains(link.Game.GameHash))
            .Include(link => link.Game)
            .ToListAsync(cancellationToken);
    }

    public async Task AddGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        await dbContext.Games.AddAsync(game, cancellationToken);
    }

    public async Task AddUserDatabaseGameAsync(UserDatabaseGame userDatabaseGame, CancellationToken cancellationToken = default)
    {
        await dbContext.UserDatabaseGames.AddAsync(userDatabaseGame, cancellationToken);
    }

    public async Task RemoveStagingGamesAsync(IReadOnlyCollection<Guid> stagingGameIds, CancellationToken cancellationToken = default)
    {
        if (stagingGameIds.Count == 0)
        {
            return;
        }

        var gamesToRemove = await dbContext.StagingGames
            .Where(g => stagingGameIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        if (gamesToRemove.Count == 0)
        {
            return;
        }

        dbContext.StagingGames.RemoveRange(gamesToRemove);
    }

    public Task MarkSessionPromotedAsync(Guid importSessionId, string ownerUserId, DateTime promotedAtUtc, CancellationToken cancellationToken = default)
    {
        return dbContext.StagingImportSessions
            .Where(s => s.Id == importSessionId && s.OwnerUserId == ownerUserId)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(s => s.PromotedAtUtc, promotedAtUtc),
                cancellationToken);
    }
}
