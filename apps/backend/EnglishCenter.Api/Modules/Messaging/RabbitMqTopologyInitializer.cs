using RabbitMQ.Client;

namespace EnglishCenter.Api.Modules.Messaging;

public sealed class RabbitMqTopologyInitializer(
    RabbitMqConnection connection,
    Microsoft.Extensions.Options.IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTopologyInitializer> logger) : IHostedService
{
    private readonly RabbitMqOptions _options = options.Value;

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

        await channel.ExchangeDeclareAsync(
            exchange: NotificationTopology.RetryExchange,
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

        var retryArguments = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = _options.RetryDelayMilliseconds,
            ["x-dead-letter-exchange"] = NotificationTopology.Exchange
        };
        await channel.QueueDeclareAsync(
            queue: NotificationTopology.RetryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: retryArguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: NotificationTopology.RetryQueue,
            exchange: NotificationTopology.RetryExchange,
            routingKey: NotificationTopology.EmailRequestedRoutingKey,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: NotificationTopology.RetryQueue,
            exchange: NotificationTopology.RetryExchange,
            routingKey: NotificationTopology.InAppRequestedRoutingKey,
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
            "RabbitMQ notification topology is ready: exchange {Exchange}, dispatch queue {DispatchQueue}, retry queue {RetryQueue}, dead-letter queue {DeadLetterQueue}.",
            NotificationTopology.Exchange,
            NotificationTopology.DispatchQueue,
            NotificationTopology.RetryQueue,
            NotificationTopology.DeadLetterQueue);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
