using System.ComponentModel.DataAnnotations;

namespace EnglishCenter.Api.Modules.Messaging;

public sealed class PublishNotificationRequest
{
    [Range(1, long.MaxValue)]
    public long RecipientUserId { get; init; }

    [Required]
    [RegularExpression("^(EMAIL|IN_APP)$")]
    public string? Channel { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    [RegularExpression("^[a-z0-9]+(?:[.-][a-z0-9]+)*$")]
    public string? TemplateKey { get; init; }

    [Required]
    public Dictionary<string, string>? Parameters { get; init; } = [];
}

public sealed record NotificationAcceptedResponse(
    Guid EventId,
    string RoutingKey,
    DateTimeOffset AcceptedAtUtc);

public sealed record NotificationRequestedIntegrationEvent(
    int SchemaVersion,
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    long RecipientUserId,
    string Channel,
    string TemplateKey,
    IReadOnlyDictionary<string, string> Parameters,
    string CorrelationId);
