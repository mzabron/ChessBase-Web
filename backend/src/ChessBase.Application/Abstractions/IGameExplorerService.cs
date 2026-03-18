using ChessBase.Application.Contracts;

namespace ChessBase.Application.Abstractions;

public interface IGameExplorerService
{
    Task<PagedResult<GameExplorerItemDto>> SearchAsync(GameExplorerSearchRequest request, CancellationToken cancellationToken = default);
    Task<MoveTreeResponse> GetMoveTreeAsync(MoveTreeRequest request, string ownerUserId, CancellationToken cancellationToken = default);
}
