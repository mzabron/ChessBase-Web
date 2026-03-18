namespace ChessBase.Application.Contracts;

public sealed record CreateUserDatabaseRequest(string Name, bool IsPublic);
