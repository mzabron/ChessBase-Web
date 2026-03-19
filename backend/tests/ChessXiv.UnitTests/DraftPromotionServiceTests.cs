using ChessXiv.Application.Abstractions;
using ChessXiv.Application.Abstractions.Repositories;
using ChessXiv.Application.Contracts;
using ChessXiv.Application.Services;
using ChessXiv.Domain.Entities;

namespace ChessXiv.UnitTests;

public class DraftPromotionServiceTests
{
    [Fact]
    public async Task PromoteAsync_WithOverrideMetadata_UpdatesExistingLinkMetadata()
    {
        var sessionId = Guid.NewGuid();
        var userDatabaseId = Guid.NewGuid();
        var stagingGame = CreateStagingGame(sessionId, "user-1", "hash-1", "New Event", "New Site");

        var existingLink = new UserDatabaseGame
        {
            UserDatabaseId = userDatabaseId,
            GameId = Guid.NewGuid(),
            AddedAtUtc = DateTime.UtcNow.AddDays(-1),
            Event = "Old Event",
            Site = "Old Site",
            Round = "1",
            Game = new Game
            {
                Id = Guid.NewGuid(),
                White = "Alpha",
                Black = "Beta",
                Result = "*",
                Pgn = "dummy",
                GameHash = "hash-1"
            }
        };

        var repo = new FakeDraftPromotionRepository(stagingGame, existingLink, sessionId, userDatabaseId, "user-1");
        var playerRepo = new FakePlayerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new DraftPromotionService(repo, playerRepo, unitOfWork);

        var result = await service.PromoteAsync("user-1", sessionId, userDatabaseId, DuplicateHandlingMode.OverrideMetadata);

        Assert.Equal(0, result.PromotedCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(1, result.OverriddenCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal("New Event", existingLink.Event);
        Assert.Equal("New Site", existingLink.Site);
    }

    [Fact]
    public async Task PromoteAsync_WithKeepExisting_DoesNotUpdateExistingMetadata()
    {
        var sessionId = Guid.NewGuid();
        var userDatabaseId = Guid.NewGuid();
        var stagingGame = CreateStagingGame(sessionId, "user-1", "hash-1", "New Event", "New Site");

        var existingLink = new UserDatabaseGame
        {
            UserDatabaseId = userDatabaseId,
            GameId = Guid.NewGuid(),
            AddedAtUtc = DateTime.UtcNow.AddDays(-1),
            Event = "Old Event",
            Site = "Old Site",
            Round = "1",
            Game = new Game
            {
                Id = Guid.NewGuid(),
                White = "Alpha",
                Black = "Beta",
                Result = "*",
                Pgn = "dummy",
                GameHash = "hash-1"
            }
        };

        var repo = new FakeDraftPromotionRepository(stagingGame, existingLink, sessionId, userDatabaseId, "user-1");
        var playerRepo = new FakePlayerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new DraftPromotionService(repo, playerRepo, unitOfWork);

        var result = await service.PromoteAsync("user-1", sessionId, userDatabaseId, DuplicateHandlingMode.KeepExisting);

        Assert.Equal(0, result.PromotedCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Equal(0, result.OverriddenCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal("Old Event", existingLink.Event);
        Assert.Equal("Old Site", existingLink.Site);
    }

    [Fact]
    public async Task PromoteAsync_ResolvesPlayers_ForPromotedGames()
    {
        var sessionId = Guid.NewGuid();
        var userDatabaseId = Guid.NewGuid();
        var stagingGame = CreateStagingGame(sessionId, "user-1", "hash-2", "Event", "Site");
        stagingGame.White = "Carlsen, Magnus";
        stagingGame.Black = "Nakamura, Hikaru";

        var repo = new FakeDraftPromotionRepository(stagingGame, null, sessionId, userDatabaseId, "user-1");
        var playerRepo = new FakePlayerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new DraftPromotionService(repo, playerRepo, unitOfWork);

        var result = await service.PromoteAsync("user-1", sessionId, userDatabaseId, DuplicateHandlingMode.KeepExisting);

        Assert.Equal(1, result.PromotedCount);
        Assert.Single(repo.AddedGames);
        var promoted = repo.AddedGames[0];
        Assert.True(promoted.WhitePlayerId.HasValue);
        Assert.True(promoted.BlackPlayerId.HasValue);
        Assert.Equal(2, playerRepo.AddedPlayers.Count);
    }

    [Fact]
    public async Task PromoteAsync_ProcessesThreeNonEmptyBatches_For1001Games()
    {
        var sessionId = Guid.NewGuid();
        var userDatabaseId = Guid.NewGuid();
        var games = Enumerable.Range(1, 1001)
            .Select(i => CreateStagingGame(sessionId, "user-1", $"hash-{i}", "Event", "Site"))
            .ToArray();

        var repo = new FakeDraftPromotionRepository(games, null, sessionId, userDatabaseId, "user-1");
        var playerRepo = new FakePlayerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new DraftPromotionService(repo, playerRepo, unitOfWork);

        var result = await service.PromoteAsync("user-1", sessionId, userDatabaseId, DuplicateHandlingMode.KeepExisting);

        Assert.Equal(1001, result.PromotedCount);
        Assert.Equal(3, repo.NonEmptyPageFetchCount);
        Assert.Equal(4, repo.PageFetchCount);
        Assert.Equal(0, repo.RemainingStagingGamesCount);
    }

    private static StagingGame CreateStagingGame(Guid sessionId, string ownerUserId, string hash, string @event, string site)
    {
        return new StagingGame
        {
            Id = Guid.NewGuid(),
            ImportSessionId = sessionId,
            OwnerUserId = ownerUserId,
            White = "Alpha",
            Black = "Beta",
            Result = "*",
            Pgn = "1. e4 e5 *",
            MoveCount = 1,
            GameHash = hash,
            Event = @event,
            Site = site,
            Round = "3",
            Moves =
            [
                new StagingMove
                {
                    Id = Guid.NewGuid(),
                    MoveNumber = 1,
                    WhiteMove = "e4",
                    BlackMove = "e5"
                }
            ],
            Positions =
            [
                new StagingPosition
                {
                    Id = Guid.NewGuid(),
                    PlyCount = 0,
                    Fen = "startpos",
                    FenHash = 123,
                    SideToMove = 'w'
                }
            ]
        };
    }

    private sealed class FakeDraftPromotionRepository : IDraftPromotionRepository
    {
        private readonly Guid _sessionId;
        private readonly Guid _userDatabaseId;
        private readonly string _ownerUserId;
        private readonly List<StagingGame> _stagingGames;
        private readonly List<UserDatabaseGame> _existingLinks;

        public FakeDraftPromotionRepository(StagingGame stagingGame, UserDatabaseGame? existingLink, Guid sessionId, Guid userDatabaseId, string ownerUserId)
            : this([stagingGame], existingLink is null ? null : [existingLink], sessionId, userDatabaseId, ownerUserId)
        {
        }

        public FakeDraftPromotionRepository(
            IReadOnlyCollection<StagingGame> stagingGames,
            UserDatabaseGame? existingLink,
            Guid sessionId,
            Guid userDatabaseId,
            string ownerUserId)
            : this(stagingGames, existingLink is null ? null : [existingLink], sessionId, userDatabaseId, ownerUserId)
        {
        }

        private FakeDraftPromotionRepository(
            IReadOnlyCollection<StagingGame> stagingGames,
            IReadOnlyCollection<UserDatabaseGame>? existingLinks,
            Guid sessionId,
            Guid userDatabaseId,
            string ownerUserId)
        {
            _sessionId = sessionId;
            _userDatabaseId = userDatabaseId;
            _ownerUserId = ownerUserId;
            _stagingGames = stagingGames.ToList();
            _existingLinks = existingLinks?.ToList() ?? [];
        }

        public List<Game> AddedGames { get; } = [];
        public int PageFetchCount { get; private set; }
        public int NonEmptyPageFetchCount { get; private set; }
        public int RemainingStagingGamesCount => _stagingGames.Count;

        public Task<StagingImportSession?> GetSessionAsync(Guid importSessionId, string ownerUserId, CancellationToken cancellationToken = default)
        {
            if (importSessionId != _sessionId || ownerUserId != _ownerUserId)
            {
                return Task.FromResult<StagingImportSession?>(null);
            }

            return Task.FromResult<StagingImportSession?>(new StagingImportSession
            {
                Id = _sessionId,
                OwnerUserId = _ownerUserId,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            });
        }

        public Task<UserDatabase?> GetUserDatabaseAsync(Guid userDatabaseId, CancellationToken cancellationToken = default)
        {
            if (userDatabaseId != _userDatabaseId)
            {
                return Task.FromResult<UserDatabase?>(null);
            }

            return Task.FromResult<UserDatabase?>(new UserDatabase
            {
                Id = _userDatabaseId,
                OwnerUserId = _ownerUserId,
                Name = "db",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        public Task<IReadOnlyCollection<StagingGame>> GetStagingGamesPageAsync(Guid importSessionId, string ownerUserId, int take, CancellationToken cancellationToken = default)
        {
            PageFetchCount++;
            var page = _stagingGames.Take(take).ToArray();
            if (page.Length > 0)
            {
                NonEmptyPageFetchCount++;
            }

            return Task.FromResult<IReadOnlyCollection<StagingGame>>(page);
        }

        public Task<IReadOnlyCollection<UserDatabaseGame>> GetExistingLinksByGameHashesAsync(Guid userDatabaseId, IReadOnlyCollection<string> gameHashes, CancellationToken cancellationToken = default)
        {
            var links = _existingLinks
                .Where(x => gameHashes.Contains(x.Game.GameHash, StringComparer.Ordinal))
                .ToArray();

            return Task.FromResult<IReadOnlyCollection<UserDatabaseGame>>(links);
        }

        public Task AddGameAsync(Game game, CancellationToken cancellationToken = default)
        {
            AddedGames.Add(game);
            return Task.CompletedTask;
        }

        public Task AddUserDatabaseGameAsync(UserDatabaseGame userDatabaseGame, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveStagingGamesAsync(IReadOnlyCollection<Guid> stagingGameIds, CancellationToken cancellationToken = default)
        {
            _stagingGames.RemoveAll(g => stagingGameIds.Contains(g.Id));
            return Task.CompletedTask;
        }

        public Task MarkSessionPromotedAsync(Guid importSessionId, string ownerUserId, DateTime promotedAtUtc, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakePlayerRepository : IPlayerRepository
    {
        private readonly Dictionary<string, Player> _players = new(StringComparer.Ordinal);
        public List<Player> AddedPlayers { get; } = [];

        public Task<IReadOnlyDictionary<string, Player>> GetByNormalizedFullNamesAsync(IReadOnlyCollection<string> normalizedFullNames, CancellationToken cancellationToken = default)
        {
            var result = _players
                .Where(kvp => normalizedFullNames.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

            return Task.FromResult<IReadOnlyDictionary<string, Player>>(result);
        }

        public Task AddRangeAsync(IReadOnlyCollection<Player> players, CancellationToken cancellationToken = default)
        {
            foreach (var player in players)
            {
                _players[player.NormalizedFullName] = player;
                AddedPlayers.Add(player);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<Guid>> SearchIdsAsync(string? normalizedFirstName, string? normalizedLastName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<Guid>>(Array.Empty<Guid>());
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());
        }

        public void ClearTracker()
        {
        }
    }

    private sealed class FakeTransaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
