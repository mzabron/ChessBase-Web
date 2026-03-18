using Microsoft.AspNetCore.Identity;

namespace ChessBase.Infrastructure.Data;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
