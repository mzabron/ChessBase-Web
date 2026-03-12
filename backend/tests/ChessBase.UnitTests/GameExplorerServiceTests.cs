using ChessBase.Application.Abstractions.Repositories;
using ChessBase.Application.Contracts;
using ChessBase.Application.Services;
using ChessBase.Domain.Engine.Abstractions;
using ChessBase.Domain.Engine.Models;

namespace ChessBase.UnitTests;

public class GameExplorerServiceTests
{
    [Fact]
    public async Task SearchAsync_NormalizesPlayerTerms_AndPassesResolvedIdsToRepository()
    {
        var expectedPlayerId = Guid.NewGuid();
        var playerRepository = new FakePlayerRepository
        {
            SearchIdsResult = [expectedPlayerId]
        };

        var explorerRepository = new FakeGameExplorerRepository
        {
            Response = new PagedResult<GameExplorerItemDto>
            {
                TotalCount = 1,
                Items = [new GameExplorerItemDto { GameId = Guid.NewGuid(), White = "Magnus Carlsen", Black = "Ian Nepomniachtchi", Result = "1-0" }]
            }
        };

        var service = new GameExplorerService(
            explorerRepository,
            playerRepository,
            new FakeBoardStateSerializer(),
            new FakePositionHasher());

        var request = new GameExplorerSearchRequest
        {
            WhiteFirstName = "  MAGNUS ",
            WhiteLastName = "CARLSEN"
        };

        var result = await service.SearchAsync(request);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("magnus", playerRepository.LastFirstName);
        Assert.Equal("carlsen", playerRepository.LastLastName);
        Assert.Single(explorerRepository.LastWhitePlayerIds!);
        Assert.Contains(expectedPlayerId, explorerRepository.LastWhitePlayerIds!);
    }

    [Fact]
    public async Task SearchAsync_ComputesFenHash_ForExactSearch()
    {
        const string fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        var playerRepository = new FakePlayerRepository();
        var explorerRepository = new FakeGameExplorerRepository();
        var serializer = new FakeBoardStateSerializer();
        var hasher = new FakePositionHasher { HashToReturn = 42UL };

        var service = new GameExplorerService(explorerRepository, playerRepository, serializer, hasher);

        await service.SearchAsync(new GameExplorerSearchRequest
        {
            SearchByPosition = true,
            PositionMode = PositionSearchMode.Exact,
            Fen = fen
        });

        Assert.Equal(fen, serializer.LastFenInput);
        Assert.Equal(unchecked((long)42UL), explorerRepository.LastFenHash);
        Assert.Equal(fen, explorerRepository.LastNormalizedFen);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenNoPlayerIdsFound_AndSkipsGameRepository()
    {
        var playerRepository = new FakePlayerRepository
        {
            SearchIdsResult = []
        };

        var explorerRepository = new FakeGameExplorerRepository();
        var service = new GameExplorerService(
            explorerRepository,
            playerRepository,
            new FakeBoardStateSerializer(),
            new FakePositionHasher());

        var result = await service.SearchAsync(new GameExplorerSearchRequest
        {
            WhiteLastName = "Carlsen"
        });

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
        Assert.Equal(0, explorerRepository.CallCount);
    }

    private sealed class FakeGameExplorerRepository : IGameExplorerRepository
    {
        public int CallCount { get; private set; }
        public IReadOnlyCollection<Guid>? LastWhitePlayerIds { get; private set; }
        public IReadOnlyCollection<Guid>? LastBlackPlayerIds { get; private set; }
        public string? LastNormalizedFen { get; private set; }
        public long? LastFenHash { get; private set; }
        public PagedResult<GameExplorerItemDto> Response { get; set; } = new();

        public Task<PagedResult<GameExplorerItemDto>> SearchAsync(
            GameExplorerSearchRequest request,
            IReadOnlyCollection<Guid>? whitePlayerIds,
            IReadOnlyCollection<Guid>? blackPlayerIds,
            string? normalizedFen,
            long? fenHash,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastWhitePlayerIds = whitePlayerIds;
            LastBlackPlayerIds = blackPlayerIds;
            LastNormalizedFen = normalizedFen;
            LastFenHash = fenHash;
            return Task.FromResult(Response);
        }
    }

    private sealed class FakePlayerRepository : IPlayerRepository
    {
        public IReadOnlyCollection<Guid> SearchIdsResult { get; set; } = [];
        public string? LastFirstName { get; private set; }
        public string? LastLastName { get; private set; }

        public Task<IReadOnlyDictionary<string, ChessBase.Domain.Entities.Player>> GetByNormalizedFullNamesAsync(
            IReadOnlyCollection<string> normalizedFullNames,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<string, ChessBase.Domain.Entities.Player>>(
                new Dictionary<string, ChessBase.Domain.Entities.Player>(StringComparer.Ordinal));
        }

        public Task AddRangeAsync(IReadOnlyCollection<ChessBase.Domain.Entities.Player> players, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<Guid>> SearchIdsAsync(
            string? normalizedFirstName,
            string? normalizedLastName,
            CancellationToken cancellationToken = default)
        {
            LastFirstName = normalizedFirstName;
            LastLastName = normalizedLastName;
            return Task.FromResult(SearchIdsResult);
        }
    }

    private sealed class FakeBoardStateSerializer : IBoardStateSerializer
    {
        public string? LastFenInput { get; private set; }

        public BoardState FromFen(string fen)
        {
            LastFenInput = fen;
            return new BoardState();
        }

        public string ToFen(in BoardState state)
        {
            return string.Empty;
        }
    }

    private sealed class FakePositionHasher : IPositionHasher
    {
        public ulong HashToReturn { get; set; } = 1UL;

        public ulong Compute(in BoardState state)
        {
            return HashToReturn;
        }
    }
}
