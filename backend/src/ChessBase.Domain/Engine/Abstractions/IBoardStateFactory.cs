using ChessBase.Domain.Engine.Models;

namespace ChessBase.Domain.Engine.Abstractions;

public interface IBoardStateFactory
{
    BoardState CreateInitial();

    BoardState CreateFromFenOrInitial(string? fen);
}
