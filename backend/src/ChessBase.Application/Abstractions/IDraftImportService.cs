using ChessBase.Application.Contracts;

namespace ChessBase.Application.Abstractions;

public interface IDraftImportService
{
    Task<DraftImportResult> ImportAsync(
        TextReader reader,
        string ownerUserId,
        Guid? importSessionId = null,
        int batchSize = 500,
        CancellationToken cancellationToken = default);
}
