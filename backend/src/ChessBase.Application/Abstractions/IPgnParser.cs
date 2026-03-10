using ChessBase.Domain.Entities;

namespace ChessBase.Application.Abstractions;

public interface IPgnParser
{
    IAsyncEnumerable<Game> ParsePgnAsync(TextReader reader, CancellationToken cancellationToken = default);
}
