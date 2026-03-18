namespace ChessBase.Api.Authentication;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ChessBase.Api";
    public string Audience { get; set; } = "ChessBase.Web";
    public string SigningKey { get; set; } = null!;
    public int ExpirationMinutes { get; set; } = 60;
}
