using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EnglishCenter.Api.Modules.Messaging;

public sealed class NotificationWorker(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    private const string RetryCountHeader = "x-retry-count";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbitConnection = await connection.GetAsync(stoppingToken);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        await using var channel = await rabbitConnection.CreateChannelAsync(channelOptions, stoppingToken);
        await channel.BasicQosAsync(0, _options.PrefetchCount, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(channel, delivery, stoppingToken);
        var consumerTag = await channel.BasicConsumeAsync(
            NotificationTopology.DispatchQueue,
            autoAck: false,
            consumer,
            stoppingToken);

        logger.LogInformation(
            "Notification worker is consuming {Queue} with prefetch {PrefetchCount} and at most {MaxRetryAttempts} retries.",
            NotificationTopology.DispatchQueue,
            _options.PrefetchCount,
            _options.MaxRetryAttempts);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            if (channel.IsOpen)
            {
                await channel.BasicCancelAsync(consumerTag, noWait: false, CancellationToken.None);
            }
        }
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken stoppingToken)
    {
        var retryCount = ReadRetryCount(delivery.BasicProperties.Headers);

        try
        {
            var integrationEvent = JsonSerializer.Deserialize<NotificationRequestedIntegrationEvent>(
                delivery.Body.Span,
                JsonOptions) ?? throw new InvalidDataException("Notification event body is empty.");

            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<NotificationMessageProcessor>();
            await processor.ProcessAsync(integrationEvent, delivery.RoutingKey, stoppingToken);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Leave the delivery unacknowledged so RabbitMQ can redeliver it after restart.
        }
        catch (Exception exception)
        {
            await HandleFailureAsync(channel, delivery, retryCount, exception, stoppingToken);
        }
    }

    private async Task HandleFailureAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        int retryCount,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (retryCount >= _options.MaxRetryAttempts)
        {
            logger.LogError(
                exception,
                "Notification message {MessageId} failed after {RetryCount} retries and will move to the dead-letter queue.",
                delivery.BasicProperties.MessageId,
                retryCount);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            return;
        }

        try
        {
            var headers = delivery.BasicProperties.Headers is null
                ? new Dictionary<string, object?>()
                : delivery.BasicProperties.Headers.ToDictionary(item => item.Key, item => item.Value);
            headers[RetryCountHeader] = retryCount + 1;
            headers["x-last-error-type"] = exception.GetType().Name;

            var retryProperties = new BasicProperties
            {
                AppId = delivery.BasicProperties.AppId,
                ContentType = delivery.BasicProperties.ContentType ?? "application/json",
                CorrelationId = delivery.BasicProperties.CorrelationId,
                DeliveryMode = DeliveryModes.Persistent,
                Headers = headers,
                MessageId = delivery.BasicProperties.MessageId,
                Timestamp = delivery.BasicProperties.Timestamp,
                Type = delivery.BasicProperties.Type
            };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.PublishTimeoutSeconds));
            await channel.BasicPublishAsync(
                exchange: NotificationTopology.RetryExchange,
                routingKey: delivery.RoutingKey,
                mandatory: true,
                basicProperties: retryProperties,
                body: delivery.Body,
                cancellationToken: timeout.Token);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);

            logger.LogWarning(
                exception,
                "Notification message {MessageId} failed and was scheduled for retry {RetryCount}/{MaxRetryAttempts}.",
                delivery.BasicProperties.MessageId,
                retryCount + 1,
                _options.MaxRetryAttempts);
        }
        catch (Exception retryException) when (
            retryException is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                retryException,
                "Could not schedule retry for notification message {MessageId}; RabbitMQ will requeue the original delivery.",
                delivery.BasicProperties.MessageId);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }

    private static int ReadRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryCountHeader, out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            byte byteValue => byteValue,
            short shortValue => shortValue,
            int intValue => intValue,
            long longValue when longValue is >= 0 and <= int.MaxValue => (int)longValue,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), CultureInfo.InvariantCulture, out var parsed) => parsed,
            ReadOnlyMemory<byte> memory when int.TryParse(Encoding.UTF8.GetString(memory.Span), CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };
    }
}
