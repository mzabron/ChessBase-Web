using ChessXiv.Application.Contracts;

namespace ChessXiv.Application.Abstractions.Repositories;

public interface IGameExplorerRepository
{
    Task<PagedResult<GameExplorerItemDto>> SearchAsync(
        GameExplorerSearchRequest request,
        IReadOnlyCollection<Guid>? whitePlayerIds,
        IReadOnlyCollection<Guid>? blackPlayerIds,
        string? normalizedFen,
        long? fenHash,
        CancellationToken cancellationToken = default);

    Task<MoveTreeResponse> GetMoveTreeAsync(
        MoveTreeRequest request,
        string ownerUserId,
        string normalizedFen,
        long fenHash,
        CancellationToken cancellationToken = default);
}
