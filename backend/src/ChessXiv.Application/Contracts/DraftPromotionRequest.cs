namespace ChessXiv.Application.Contracts;

public sealed record DraftPromotionRequest(
    Guid UserDatabaseId,
    DuplicateHandlingMode DuplicateHandling = DuplicateHandlingMode.KeepExisting);
