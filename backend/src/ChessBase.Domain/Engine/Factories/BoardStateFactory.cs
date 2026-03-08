using ChessBase.Domain.Engine.Abstractions;
using ChessBase.Domain.Engine.Models;

namespace ChessBase.Domain.Engine.Factories;

public sealed class BoardStateFactory(IBoardStateSerializer serializer) : IBoardStateFactory
{
    private const string StandardInitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public BoardState CreateInitial()
    {
        return serializer.FromFen(StandardInitialFen);
    }

    public BoardState CreateFromFenOrInitial(string? fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
        {
            return CreateInitial();
        }

        return serializer.FromFen(fen);
    }
}
