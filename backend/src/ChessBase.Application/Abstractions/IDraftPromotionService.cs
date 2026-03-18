using ChessBase.Application.Contracts;

namespace ChessBase.Application.Abstractions;

public interface IDraftPromotionService
{
    Task<DraftPromotionResult> PromoteAsync(
        string ownerUserId,
        Guid importSessionId,
        Guid userDatabaseId,
        DuplicateHandlingMode duplicateHandling,
        CancellationToken cancellationToken = default);
}
