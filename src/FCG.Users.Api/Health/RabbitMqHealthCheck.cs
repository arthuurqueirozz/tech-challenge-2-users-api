using FCG.Users.Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FCG.Users.Api.Health;

public sealed class RabbitMqHealthCheck(
    IOptions<RabbitMqOptions> options)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rabbit = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = rabbit.Host,
                Port = rabbit.Port,
                VirtualHost = rabbit.VirtualHost,
                UserName = rabbit.Username,
                Password = rabbit.Password,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(3),
                AutomaticRecoveryEnabled = false
            };

            await using var connection = await factory
                .CreateConnectionAsync(cancellationToken);

            return HealthCheckResult.Healthy("RabbitMQ is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "RabbitMQ is unavailable.",
                exception);
        }
    }
}
