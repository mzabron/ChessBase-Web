using ChessXiv.Application.Contracts;

namespace ChessXiv.Application.Abstractions;

public interface IDraftImportService
{
    Task<DraftImportResult> ImportAsync(
        TextReader reader,
        string ownerUserId,
        Guid? importSessionId = null,
        int batchSize = 500,
        CancellationToken cancellationToken = default);
}
