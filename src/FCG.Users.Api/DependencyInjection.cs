using System.Text;
using Amazon;
using Amazon.SQS;
using FCG.Users.Api.Configuration;
using FCG.Users.Api.Messaging;
using FCG.Users.Api.Services;
using FCG.Users.Api.Shared;
using FCG.Users.Api.Validation;
using FCG.Users.Infrastructure;
using FCG.Users.Infrastructure.Configuration;
using FCG.Users.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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
            .AddOptions<SqsOptions>()
            .Bind(configuration.GetSection(SqsOptions.SectionName))
            .Validate(options => options.HasValidQueueUrl(),
                "Sqs:QueueUrl must be an HTTPS SQS queue URL in Sqs:Region.")
            .ValidateOnStart();

        services.AddSingleton<IAmazonSQS>(provider => new AmazonSQSClient(new AmazonSQSConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(provider.GetRequiredService<IOptions<SqsOptions>>().Value.Region),
            Timeout = TimeSpan.FromSeconds(10),
            MaxErrorRetry = 2
        }));

        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserCreatedPublisher, SqsUserCreatedPublisher>();
        services.AddHostedService<OutboxPublisherBackgroundService>();

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
                Description = "Informe apenas o JWT. O Swagger UI adiciona o prefixo Bearer automaticamente.",
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
                tags: ["ready"],
                customTestQuery: async (context, cancellationToken) =>
                    await context.Database.CanConnectAsync(cancellationToken)
                    && !(await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any());

        return services;
    }
}
