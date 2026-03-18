namespace ChessBase.Application.Contracts;

public sealed record AddGamesToDatabaseRequest(IReadOnlyCollection<Guid> GameIds);
