using FCG.Users.Api;
using FCG.Users.Api.Endpoints;
using FCG.Users.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

builder.Services.AddUsersApi(builder.Configuration);

var app = builder.Build();

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
.WithName("GetServiceStatus")
.WithOpenApi();

app.MapHealthChecks("/health");
app.MapMetrics();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapAuthEndpoints();
app.MapProfileEndpoints();

await app.Services.InitialiseUsersDatabaseAsync();
await app.RunAsync();

public partial class Program;
