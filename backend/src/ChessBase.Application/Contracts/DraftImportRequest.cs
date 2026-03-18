namespace ChessBase.Application.Contracts;

public sealed record DraftImportRequest(string Pgn, Guid? ImportSessionId = null);
