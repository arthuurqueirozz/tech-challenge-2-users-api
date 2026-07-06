using System.Text;
using FCG.Users.Api.Configuration;
using FCG.Users.Api.Messaging;
using FCG.Users.Api.Services;
using FCG.Users.Api.Shared;
using FCG.Users.Api.Validation;
using FCG.Users.Infrastructure;
using FCG.Users.Infrastructure.Configuration;
using FCG.Users.Infrastructure.Persistence;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace FCG.Users.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddUsersInfrastructure(configuration);

        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host),
                "RabbitMq:Host is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.VirtualHost),
                "RabbitMq:VirtualHost is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Username),
                "RabbitMq:Username is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password),
                "RabbitMq:Password is required.")
            .ValidateOnStart();

        services.AddMassTransit(configurator =>
        {
            configurator.UsingRabbitMq((context, rabbit) =>
            {
                var options = context
                    .GetRequiredService<IOptions<RabbitMqOptions>>()
                    .Value;

                rabbit.Host(
                    options.Host,
                    options.Port,
                    options.VirtualHost,
                    host =>
                    {
                        host.Username(options.Username);
                        host.Password(options.Password);
                    });
            });
        });

        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserCreatedPublisher, MassTransitUserCreatedPublisher>();

        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.Key)),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "FIAP Cloud Games Users API",
                Version = "v1",
                Description = "Cadastro, autenticacao JWT e autorizacao de usuarios."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Informe o JWT no formato: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            options.OperationFilter<AuthorizationOperationFilter>();
        });

        services
            .AddHealthChecks()
            .AddDbContextCheck<UsersDbContext>(
                "users-sqlserver",
                tags: ["ready"]);

        return services;
    }
}
