using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Modules.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Messaging;

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = AuthorizationPolicies.StaffOperations)]
public sealed class NotificationsController(IIntegrationEventPublisher publisher) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<NotificationAcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<NotificationAcceptedResponse>> Publish(
        PublishNotificationRequest request,
        CancellationToken cancellationToken)
    {
        ValidateParameters(request.Parameters!);

        var eventId = Guid.NewGuid();
        var acceptedAtUtc = DateTimeOffset.UtcNow;
        var channel = request.Channel!;
        var routingKey = NotificationTopology.RoutingKeyFor(channel);
        var integrationEvent = new NotificationRequestedIntegrationEvent(
            SchemaVersion: 1,
            EventId: eventId,
            OccurredAtUtc: acceptedAtUtc,
            RecipientUserId: request.RecipientUserId,
            Channel: channel,
            TemplateKey: request.TemplateKey!,
            Parameters: request.Parameters!,
            CorrelationId: HttpContext.TraceIdentifier);

        await publisher.PublishAsync(routingKey, integrationEvent, eventId.ToString(), cancellationToken);
        return Accepted(new NotificationAcceptedResponse(eventId, routingKey, acceptedAtUtc));
    }

    private static void ValidateParameters(IReadOnlyDictionary<string, string> parameters)
    {
        var errors = new Dictionary<string, string[]>();
        if (parameters.Count > 20)
        {
            errors[nameof(PublishNotificationRequest.Parameters)] = ["At most 20 template parameters are allowed."];
        }
        else if (parameters.Any(item =>
                     string.IsNullOrWhiteSpace(item.Key) ||
                     item.Key.Length > 50 ||
                     item.Value is null ||
                     item.Value.Length > 500))
        {
            errors[nameof(PublishNotificationRequest.Parameters)] =
                ["Parameter names must contain 1-50 characters and values must contain at most 500 characters."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException("Notification parameters are invalid.", errors);
        }
    }
}
