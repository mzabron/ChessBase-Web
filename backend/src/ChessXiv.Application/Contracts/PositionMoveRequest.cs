namespace ChessXiv.Application.Contracts;

public class PositionMoveRequest
{
    public string Fen { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string? Promotion { get; set; }
}
