using ChessBase.Domain.Entities;

namespace ChessBase.Application.Abstractions.Repositories;

public interface IDraftPromotionRepository
{
    Task<StagingImportSession?> GetSessionAsync(Guid importSessionId, string ownerUserId, CancellationToken cancellationToken = default);
    Task<UserDatabase?> GetUserDatabaseAsync(Guid userDatabaseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<StagingGame>> GetStagingGamesPageAsync(
        Guid importSessionId,
        string ownerUserId,
        int take,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserDatabaseGame>> GetExistingLinksByGameHashesAsync(
        Guid userDatabaseId,
        IReadOnlyCollection<string> gameHashes,
        CancellationToken cancellationToken = default);
    Task AddGameAsync(Game game, CancellationToken cancellationToken = default);
    Task AddUserDatabaseGameAsync(UserDatabaseGame userDatabaseGame, CancellationToken cancellationToken = default);
    Task RemoveStagingGamesAsync(IReadOnlyCollection<Guid> stagingGameIds, CancellationToken cancellationToken = default);
    Task MarkSessionPromotedAsync(Guid importSessionId, string ownerUserId, DateTime promotedAtUtc, CancellationToken cancellationToken = default);
}
