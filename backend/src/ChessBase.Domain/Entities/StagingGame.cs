namespace ChessBase.Domain.Entities;

public class StagingGame
{
    public Guid Id { get; set; }
    public Guid ImportSessionId { get; set; }
    public string OwnerUserId { get; set; } = null!;
    public Guid? WhitePlayerId { get; set; }
    public Guid? BlackPlayerId { get; set; }
    public DateTime? Date { get; set; }
    public int Year { get; set; }
    public string? Round { get; set; }
    public string? WhiteTitle { get; set; }
    public string? BlackTitle { get; set; }
    public int? WhiteElo { get; set; }
    public int? BlackElo { get; set; }
    public string? Event { get; set; }
    public string? Site { get; set; }
    public string? TimeControl { get; set; }
    public string? ECO { get; set; }
    public string? Opening { get; set; }
    public string White { get; set; } = null!;
    public string Black { get; set; } = null!;
    public string Result { get; set; } = null!;
    public string Pgn { get; set; } = null!;
    public int MoveCount { get; set; }
    public string GameHash { get; set; } = string.Empty;

    public StagingImportSession ImportSession { get; set; } = null!;
    public ICollection<StagingMove> Moves { get; set; } = new List<StagingMove>();
    public ICollection<StagingPosition> Positions { get; set; } = new List<StagingPosition>();
}
