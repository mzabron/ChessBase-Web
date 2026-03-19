using ChessXiv.Application.Abstractions;
using ChessXiv.Application.Abstractions.Repositories;
using ChessXiv.Application.Contracts;
using ChessXiv.Domain.Entities;

namespace ChessXiv.Application.Services;

public sealed class DraftPromotionService(
    IDraftPromotionRepository draftPromotionRepository,
    IPlayerRepository playerRepository,
    IUnitOfWork unitOfWork) : IDraftPromotionService
{
    private const int PromotionBatchSize = 500;

    public async Task<DraftPromotionResult> PromoteAsync(
        string ownerUserId,
        Guid importSessionId,
        Guid userDatabaseId,
        DuplicateHandlingMode duplicateHandling,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));
        }

        var session = await draftPromotionRepository.GetSessionAsync(importSessionId, ownerUserId, cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException("Draft import session was not found for this user.");
        }

        if (session.PromotedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Draft import session has already been promoted.");
        }

        var userDatabase = await draftPromotionRepository.GetUserDatabaseAsync(userDatabaseId, cancellationToken);

        if (userDatabase is null)
        {
            throw new InvalidOperationException("Target user database was not found.");
        }

        if (!string.Equals(userDatabase.OwnerUserId, ownerUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Target user database does not belong to the current user.");
        }

        var now = DateTime.UtcNow;
        var promotedCount = 0;
        var duplicateCount = 0;
        var overriddenCount = 0;
        var skippedCount = 0;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            while (true)
            {
                var stagingPage = await draftPromotionRepository
                    .GetStagingGamesPageAsync(importSessionId, ownerUserId, PromotionBatchSize, cancellationToken);

                if (stagingPage.Count == 0)
                {
                    break;
                }

                var pageHashes = stagingPage
                    .Select(g => g.GameHash)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                var existingLinks = await draftPromotionRepository
                    .GetExistingLinksByGameHashesAsync(userDatabaseId, pageHashes, cancellationToken);

                var existingByHash = existingLinks
                    .GroupBy(link => link.Game.GameHash, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

                var gamesToPromote = new List<Game>(stagingPage.Count);
                var stagingIdsToDelete = new List<Guid>(stagingPage.Count);

                foreach (var stagingGame in stagingPage)
                {
                    if (existingByHash.TryGetValue(stagingGame.GameHash, out var existingLink))
                    {
                        duplicateCount++;

                        if (duplicateHandling == DuplicateHandlingMode.OverrideMetadata)
                        {
                            ApplyJoinMetadata(existingLink, stagingGame, now);
                            overriddenCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }

                        stagingIdsToDelete.Add(stagingGame.Id);
                        continue;
                    }

                    var promotedGame = MapToMainGame(stagingGame);
                    gamesToPromote.Add(promotedGame);
                    stagingIdsToDelete.Add(stagingGame.Id);
                }

                if (gamesToPromote.Count > 0)
                {
                    await ResolvePlayersAsync(gamesToPromote, cancellationToken);

                    foreach (var promotedGame in gamesToPromote)
                    {
                        await draftPromotionRepository.AddGameAsync(promotedGame, cancellationToken);
                        await draftPromotionRepository.AddUserDatabaseGameAsync(new UserDatabaseGame
                        {
                            UserDatabaseId = userDatabaseId,
                            GameId = promotedGame.Id,
                            AddedAtUtc = now,
                            Date = promotedGame.Date,
                            Year = promotedGame.Year,
                            Event = promotedGame.Event,
                            Round = promotedGame.Round,
                            Site = promotedGame.Site
                        }, cancellationToken);

                        promotedCount++;
                    }
                }

                await draftPromotionRepository.RemoveStagingGamesAsync(stagingIdsToDelete, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                unitOfWork.ClearTracker();
            }

            await draftPromotionRepository.MarkSessionPromotedAsync(importSessionId, ownerUserId, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new DraftPromotionResult(importSessionId, promotedCount, duplicateCount, overriddenCount, skippedCount);
    }

    private async Task ResolvePlayersAsync(IReadOnlyCollection<Game> games, CancellationToken cancellationToken)
    {
        var normalizedToOriginalName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in games.SelectMany(g => new[] { g.White, g.Black }))
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var normalizedName = PlayerNameNormalizer.Normalize(name);
            if (normalizedName.Length == 0 || normalizedToOriginalName.ContainsKey(normalizedName))
            {
                continue;
            }

            normalizedToOriginalName[normalizedName] = name;
        }

        var normalizedNames = normalizedToOriginalName.Keys.ToArray();
        if (normalizedNames.Length == 0)
        {
            return;
        }

        var existingPlayers = await playerRepository.GetByNormalizedFullNamesAsync(normalizedNames, cancellationToken);
        var playersByNormalizedName = new Dictionary<string, Player>(existingPlayers, StringComparer.Ordinal);
        var missingPlayers = new List<Player>();

        foreach (var normalizedName in normalizedNames)
        {
            if (playersByNormalizedName.ContainsKey(normalizedName))
            {
                continue;
            }

            var fullName = normalizedToOriginalName[normalizedName];
            var (firstName, lastName) = PlayerNameNormalizer.ParseNameParts(fullName);

            var player = new Player
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                NormalizedFullName = normalizedName,
                FirstName = firstName,
                LastName = lastName,
                NormalizedFirstName = firstName is null ? null : PlayerNameNormalizer.Normalize(firstName),
                NormalizedLastName = lastName is null ? null : PlayerNameNormalizer.Normalize(lastName)
            };

            missingPlayers.Add(player);
        }

        if (missingPlayers.Count > 0)
        {
            await playerRepository.AddRangeAsync(missingPlayers, cancellationToken);
            foreach (var player in missingPlayers)
            {
                playersByNormalizedName[player.NormalizedFullName] = player;
            }
        }

        foreach (var game in games)
        {
            var whiteNormalizedName = PlayerNameNormalizer.Normalize(game.White);
            var blackNormalizedName = PlayerNameNormalizer.Normalize(game.Black);

            if (playersByNormalizedName.TryGetValue(whiteNormalizedName, out var whitePlayer))
            {
                game.WhitePlayerId = whitePlayer.Id;
            }

            if (playersByNormalizedName.TryGetValue(blackNormalizedName, out var blackPlayer))
            {
                game.BlackPlayerId = blackPlayer.Id;
            }
        }
    }

    private static void ApplyJoinMetadata(UserDatabaseGame link, StagingGame stagingGame, DateTime now)
    {
        link.AddedAtUtc = now;
        link.Date = stagingGame.Date;
        link.Year = stagingGame.Year;
        link.Event = stagingGame.Event;
        link.Round = stagingGame.Round;
        link.Site = stagingGame.Site;
    }

    private static Game MapToMainGame(StagingGame stagingGame)
    {
        return new Game
        {
            Id = Guid.NewGuid(),
            WhitePlayerId = stagingGame.WhitePlayerId,
            BlackPlayerId = stagingGame.BlackPlayerId,
            Date = stagingGame.Date,
            Year = stagingGame.Year,
            Round = stagingGame.Round,
            WhiteTitle = stagingGame.WhiteTitle,
            BlackTitle = stagingGame.BlackTitle,
            WhiteElo = stagingGame.WhiteElo,
            BlackElo = stagingGame.BlackElo,
            Event = stagingGame.Event,
            Site = stagingGame.Site,
            TimeControl = stagingGame.TimeControl,
            ECO = stagingGame.ECO,
            Opening = stagingGame.Opening,
            White = stagingGame.White,
            Black = stagingGame.Black,
            Result = stagingGame.Result,
            Pgn = stagingGame.Pgn,
            MoveCount = stagingGame.MoveCount,
            GameHash = stagingGame.GameHash,
            Moves = stagingGame.Moves.Select(m => new Move
            {
                Id = Guid.NewGuid(),
                MoveNumber = m.MoveNumber,
                WhiteMove = m.WhiteMove,
                BlackMove = m.BlackMove,
                WhiteClk = m.WhiteClk,
                BlackClk = m.BlackClk,
                WhiteEval = m.WhiteEval,
                BlackEval = m.BlackEval
            }).ToArray(),
            Positions = stagingGame.Positions.Select(p => new Position
            {
                Id = Guid.NewGuid(),
                Fen = p.Fen,
                FenHash = p.FenHash,
                PlyCount = p.PlyCount,
                LastMove = p.LastMove,
                SideToMove = p.SideToMove
            }).ToArray()
        };
    }
}
