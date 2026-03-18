namespace ChessBase.Application.Contracts;

public sealed record DraftPromotionResult(
    Guid ImportSessionId,
    int PromotedCount,
    int DuplicateCount,
    int OverriddenCount,
    int SkippedCount);
