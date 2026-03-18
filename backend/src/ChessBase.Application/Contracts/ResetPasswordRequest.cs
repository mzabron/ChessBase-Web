namespace ChessBase.Application.Contracts;

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
