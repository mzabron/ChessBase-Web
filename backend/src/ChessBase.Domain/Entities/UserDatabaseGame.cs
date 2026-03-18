namespace ChessBase.Domain.Entities;

public class UserDatabaseGame
{
    public Guid UserDatabaseId { get; set; }
    public Guid GameId { get; set; }
    public DateTime AddedAtUtc { get; set; }

    public UserDatabase UserDatabase { get; set; } = null!;
    public Game Game { get; set; } = null!;
}
