using ChessBase.Application.Abstractions.Repositories;
using ChessBase.Domain.Entities;
using ChessBase.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ChessBase.Infrastructure.Repositories;

public sealed class DraftImportRepository(ChessBaseDbContext dbContext) : IDraftImportRepository
{
    public Task<StagingImportSession?> GetImportSessionAsync(Guid importSessionId, string ownerUserId, CancellationToken cancellationToken = default)
    {
        return dbContext.StagingImportSessions
            .FirstOrDefaultAsync(s => s.Id == importSessionId && s.OwnerUserId == ownerUserId, cancellationToken);
    }

    public async Task AddImportSessionAsync(StagingImportSession session, CancellationToken cancellationToken = default)
    {
        await dbContext.StagingImportSessions.AddAsync(session, cancellationToken);
    }

    public async Task AddStagingGamesAsync(IReadOnlyCollection<StagingGame> games, CancellationToken cancellationToken = default)
    {
        if (games.Count == 0)
        {
            return;
        }

        await dbContext.StagingGames.AddRangeAsync(games, cancellationToken);
    }
}
