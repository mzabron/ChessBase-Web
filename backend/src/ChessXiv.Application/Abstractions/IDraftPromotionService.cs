using ChessXiv.Application.Contracts;

namespace ChessXiv.Application.Abstractions;

public interface IDraftPromotionService
{
    Task<DraftPromotionResult> PromoteAsync(
        string ownerUserId,
        Guid importSessionId,
        Guid userDatabaseId,
        DuplicateHandlingMode duplicateHandling,
        CancellationToken cancellationToken = default);
}
