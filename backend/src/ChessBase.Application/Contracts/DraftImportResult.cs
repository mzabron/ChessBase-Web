namespace ChessBase.Application.Contracts;

public sealed record DraftImportResult(
    Guid ImportSessionId,
    int ParsedCount,
    int ImportedCount,
    int SkippedCount,
    DateTime ExpiresAtUtc);
