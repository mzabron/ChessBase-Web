using ChessBase.Application.Contracts;
using ChessBase.Infrastructure.Data;

namespace ChessBase.Api.Authentication;

public interface IJwtTokenService
{
    AuthTokenResponse CreateToken(ApplicationUser user);
}
