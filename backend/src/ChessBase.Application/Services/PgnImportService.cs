using ChessBase.Application.Abstractions;
using ChessBase.Application.Abstractions.Repositories;
using ChessBase.Application.Contracts;
using ChessBase.Domain.Entities;

namespace ChessBase.Application.Services;

public class PgnImportService(
	IPgnParser pgnParser,
	IGameRepository gameRepository,
	IPositionImportCoordinator positionImportCoordinator,
	IUnitOfWork unitOfWork) : IPgnImportService
{
	public async Task<PgnImportResult> ImportAsync(TextReader reader, bool markAsMaster = false, int batchSize = 500, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(reader);

		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
		}

		var parsedCount = 0;
		var importedCount = 0;
		var skippedCount = 0;
		var batch = new List<Game>(batchSize);

		await foreach (var game in pgnParser.ParsePgnAsync(reader, cancellationToken))
		{
			parsedCount++;
			if (string.IsNullOrWhiteSpace(game.White) || string.IsNullOrWhiteSpace(game.Black))
			{
				skippedCount++;
				continue;
			}

			game.IsMaster = markAsMaster;
			batch.Add(game);
			importedCount++;

			if (batch.Count >= batchSize)
			{
				await PersistBatchAsync(batch, cancellationToken);
				batch.Clear();
			}
		}

		if (batch.Count > 0)
		{
			await PersistBatchAsync(batch, cancellationToken);
		}

		return new PgnImportResult(
			ParsedCount: parsedCount,
			ImportedCount: importedCount,
			SkippedCount: skippedCount);
	}

	private async Task PersistBatchAsync(IReadOnlyCollection<Domain.Entities.Game> games, CancellationToken cancellationToken)
	{
		await positionImportCoordinator.PopulateAsync(games, cancellationToken);
		await gameRepository.AddRangeAsync(games, cancellationToken);
		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
