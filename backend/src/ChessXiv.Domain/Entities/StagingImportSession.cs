namespace ChessXiv.Domain.Entities;

public class StagingImportSession
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? PromotedAtUtc { get; set; }

    public ICollection<StagingGame> Games { get; set; } = new List<StagingGame>();
}
