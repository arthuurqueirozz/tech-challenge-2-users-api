using System.Security.Claims;
using FCG.Users.Api.Contracts.Auth;
using FCG.Users.Api.Services;
using FCG.Users.Api.Shared;

namespace FCG.Users.Api.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/api/me/profile", GetProfileAsync)
            .RequireAuthorization()
            .WithTags("Identity")
            .WithName("GetMyProfile")
            .Produces<UserResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal,
        IUserProfileService service,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var profile = await service.GetAsync(
            userId.Value,
            cancellationToken);

        return profile is null
            ? Results.NotFound()
            : Results.Ok(profile);
    }
}
