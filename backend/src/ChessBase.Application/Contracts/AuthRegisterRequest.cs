namespace ChessBase.Application.Contracts;

public sealed record AuthRegisterRequest(string Login, string Email, string Password);
