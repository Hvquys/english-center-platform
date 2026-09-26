using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace EnglishCenter.Api.Modules.Messaging;

public sealed class RabbitMqHealthCheck(RabbitMqConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rabbitConnection = await connection.GetAsync(cancellationToken);
            await using var channel = await rabbitConnection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.ExchangeDeclarePassiveAsync(NotificationTopology.Exchange, cancellationToken);
            await channel.QueueDeclarePassiveAsync(NotificationTopology.DispatchQueue, cancellationToken);
            await channel.QueueDeclarePassiveAsync(NotificationTopology.RetryQueue, cancellationToken);
            await channel.QueueDeclarePassiveAsync(NotificationTopology.DeadLetterQueue, cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ connection and notification topology are available.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection or notification topology is unavailable.", exception);
        }
    }
}
