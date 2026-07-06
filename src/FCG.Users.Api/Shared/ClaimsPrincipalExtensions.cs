using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FCG.Users.Api.Shared;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(subject, out var userId)
            ? userId
            : null;
    }
}
