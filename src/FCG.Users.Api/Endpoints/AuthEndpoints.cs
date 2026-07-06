using FCG.Users.Api.Contracts.Auth;
using FCG.Users.Api.Services;
using FluentValidation;

namespace FCG.Users.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/auth")
            .WithTags("Auth");

        group
            .MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .WithName("RegisterUser")
            .Produces<AuthResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group
            .MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("LoginUser")
            .Produces<AuthResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IValidator<RegisterRequest> validator,
        IAuthService service,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var response = await service.RegisterAsync(request, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        IAuthService service,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var response = await service.LoginAsync(request, cancellationToken);
        return Results.Ok(response);
    }
}
