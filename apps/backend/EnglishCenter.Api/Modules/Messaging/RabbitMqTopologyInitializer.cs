using RabbitMQ.Client;

namespace EnglishCenter.Api.Modules.Messaging;

public sealed class RabbitMqTopologyInitializer(
    RabbitMqConnection connection,
    ILogger<RabbitMqTopologyInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var rabbitConnection = await connection.GetAsync(cancellationToken);
        await using var channel = await rabbitConnection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: NotificationTopology.Exchange,
            type: NotificationTopology.ExchangeType,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: NotificationTopology.DeadLetterExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var dispatchArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = NotificationTopology.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = NotificationTopology.DeadLetterRoutingKey
        };
        await channel.QueueDeclareAsync(
            queue: NotificationTopology.DispatchQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: dispatchArguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: NotificationTopology.DispatchQueue,
            exchange: NotificationTopology.Exchange,
            routingKey: NotificationTopology.DispatchBinding,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: NotificationTopology.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: NotificationTopology.DeadLetterQueue,
            exchange: NotificationTopology.DeadLetterExchange,
            routingKey: NotificationTopology.DeadLetterRoutingKey,
            arguments: null,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "RabbitMQ notification topology is ready: exchange {Exchange}, dispatch queue {DispatchQueue}, dead-letter queue {DeadLetterQueue}.",
            NotificationTopology.Exchange,
            NotificationTopology.DispatchQueue,
            NotificationTopology.DeadLetterQueue);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
