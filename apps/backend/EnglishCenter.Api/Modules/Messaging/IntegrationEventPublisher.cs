using System.Text.Json;
using EnglishCenter.Api.Api.ErrorHandling;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EnglishCenter.Api.Modules.Messaging;

public interface IIntegrationEventPublisher
{
    Task PublishAsync<T>(string routingKey, T message, string messageId, CancellationToken cancellationToken);
}

public sealed class DisabledIntegrationEventPublisher : IIntegrationEventPublisher
{
    public Task PublishAsync<T>(
        string routingKey,
        T message,
        string messageId,
        CancellationToken cancellationToken) =>
        throw new DependencyUnavailableException(
            "RabbitMQ messaging is disabled. Enable Messaging:RabbitMq and provide local credentials.",
            new InvalidOperationException("Messaging:RabbitMq:Enabled is false."));
}

public sealed class RabbitMqIntegrationEventPublisher(
    RabbitMqConnection connection,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqIntegrationEventPublisher> logger) : IIntegrationEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _channelGate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync<T>(
        string routingKey,
        T message,
        string messageId,
        CancellationToken cancellationToken)
    {
        await _channelGate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            var properties = new BasicProperties
            {
                AppId = "EnglishCenter.Api",
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = messageId,
                Type = typeof(T).Name,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.PublishTimeoutSeconds));
            await channel.BasicPublishAsync(
                exchange: NotificationTopology.Exchange,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: timeout.Token);

            logger.LogInformation(
                "Published integration event {MessageType} with id {MessageId} and routing key {RoutingKey}.",
                typeof(T).Name,
                messageId,
                routingKey);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await ResetChannelAsync();
            throw new DependencyUnavailableException(
                "RabbitMQ could not confirm the notification request. Retry the operation later.",
                exception);
        }
        finally
        {
            _channelGate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel?.IsOpen == true)
        {
            return _channel;
        }

        await ResetChannelAsync();
        var rabbitConnection = await connection.GetAsync(cancellationToken);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        _channel = await rabbitConnection.CreateChannelAsync(channelOptions, cancellationToken);
        return _channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _channelGate.WaitAsync();
        try
        {
            await ResetChannelAsync();
        }
        finally
        {
            _channelGate.Release();
            _channelGate.Dispose();
        }
    }
}
