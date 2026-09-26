using System.Text.Json;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Messaging;

public enum NotificationProcessingResult
{
    Processed,
    AlreadyProcessed
}

public sealed class NotificationMessageProcessor(
    EnglishCenterDbContext dbContext,
    ILogger<NotificationMessageProcessor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<NotificationProcessingResult> ProcessAsync(
        NotificationRequestedIntegrationEvent integrationEvent,
        string routingKey,
        CancellationToken cancellationToken)
    {
        Validate(integrationEvent, routingKey);

        if (await dbContext.NotificationProcessingRecords
                .AsNoTracking()
                .AnyAsync(record => record.EventId == integrationEvent.EventId, cancellationToken))
        {
            logger.LogInformation(
                "Notification event {EventId} was already processed; the duplicate delivery was acknowledged.",
                integrationEvent.EventId);
            return NotificationProcessingResult.AlreadyProcessed;
        }

        dbContext.NotificationProcessingRecords.Add(new NotificationProcessingRecord
        {
            EventId = integrationEvent.EventId,
            OccurredAtUtc = integrationEvent.OccurredAtUtc,
            RecipientUserId = integrationEvent.RecipientUserId,
            Channel = integrationEvent.Channel,
            TemplateKey = integrationEvent.TemplateKey,
            ParametersJson = JsonSerializer.Serialize(integrationEvent.Parameters, JsonOptions),
            CorrelationId = integrationEvent.CorrelationId,
            ProcessedAtUtc = DateTime.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (await dbContext.NotificationProcessingRecords
                    .AsNoTracking()
                    .AnyAsync(record => record.EventId == integrationEvent.EventId, cancellationToken))
            {
                logger.LogInformation(
                    "Notification event {EventId} was concurrently processed; the duplicate delivery was acknowledged.",
                    integrationEvent.EventId);
                return NotificationProcessingResult.AlreadyProcessed;
            }

            throw;
        }

        logger.LogInformation(
            "Notification event {EventId} for user {RecipientUserId} and channel {Channel} was durably recorded.",
            integrationEvent.EventId,
            integrationEvent.RecipientUserId,
            integrationEvent.Channel);
        return NotificationProcessingResult.Processed;
    }

    private static void Validate(
        NotificationRequestedIntegrationEvent integrationEvent,
        string routingKey)
    {
        if (integrationEvent.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported notification schema version {integrationEvent.SchemaVersion}.");
        }

        if (integrationEvent.EventId == Guid.Empty ||
            integrationEvent.RecipientUserId < 1 ||
            integrationEvent.OccurredAtUtc == default ||
            string.IsNullOrWhiteSpace(integrationEvent.TemplateKey) ||
            integrationEvent.TemplateKey.Length > 100 ||
            integrationEvent.Parameters is null ||
            integrationEvent.Parameters.Count > 20 ||
            string.IsNullOrWhiteSpace(integrationEvent.CorrelationId) ||
            integrationEvent.CorrelationId.Length > 100)
        {
            throw new InvalidDataException("Notification event contains invalid required fields.");
        }

        var expectedRoutingKey = NotificationTopology.RoutingKeyFor(integrationEvent.Channel);
        if (!string.Equals(routingKey, expectedRoutingKey, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Notification routing key does not match the event channel.");
        }

        if (integrationEvent.Parameters.Any(parameter =>
                string.IsNullOrWhiteSpace(parameter.Key) ||
                parameter.Key.Length > 50 ||
                parameter.Value is null ||
                parameter.Value.Length > 500))
        {
            throw new InvalidDataException("Notification template parameters are invalid.");
        }
    }
}
