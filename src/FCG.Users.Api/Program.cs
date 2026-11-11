using FCG.Users.Api;
using FCG.Users.Api.Endpoints;
using FCG.Users.Infrastructure;
using FCG.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Prometheus;

var migrate = args.Contains("--migrate", StringComparer.Ordinal);
var hostArgs = args.Where(argument => argument != "--migrate").ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);

if (migrate)
{
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddUsersInfrastructure(builder.Configuration);
    await using var migrationApp = builder.Build();
    await migrationApp.Services.InitialiseUsersDatabaseAsync();
    return;
}

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

builder.Services.AddUsersApi(builder.Configuration);

var app = builder.Build();

var buildSha = Environment.GetEnvironmentVariable("FCG_BUILD_SHA") ?? "development";
if (buildSha != "development" && (buildSha.Length != 40 || !buildSha.All(Uri.IsHexDigit)))
{
    throw new InvalidOperationException("FCG_BUILD_SHA must be a full Git commit SHA or development.");
}
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Build-Sha"] = buildSha;
        return Task.CompletedTask;
    });
    await next(context);
});

app.UseRouting();
app.UseMiddleware<HttpMetricsMiddleware>();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => TypedResults.Ok(new
{
    service = "FCG Users API",
    status = "running"
}))
.WithName("GetServiceStatus");

app.MapHealthChecks("/health");
app.MapMetrics();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapAuthEndpoints();
app.MapProfileEndpoints();

await app.RunAsync();

public partial class Program;
