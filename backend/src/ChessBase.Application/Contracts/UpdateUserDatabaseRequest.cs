namespace ChessBase.Application.Contracts;

public sealed record UpdateUserDatabaseRequest(string Name, bool IsPublic);
