using RabbitMQ.Client;

namespace EnglishCenter.Api.Modules.Messaging;

public static class NotificationTopology
{
    public const string Exchange = "english-center.notifications";
    public const string ExchangeType = RabbitMQ.Client.ExchangeType.Topic;
    public const string DispatchQueue = "english-center.notifications.dispatch";
    public const string DispatchBinding = "notification.*.requested";

    public const string RetryExchange = "english-center.notifications.retry";
    public const string RetryQueue = "english-center.notifications.retry";

    public const string DeadLetterExchange = "english-center.notifications.dlx";
    public const string DeadLetterQueue = "english-center.notifications.dead-letter";
    public const string DeadLetterRoutingKey = "notification.dead-letter";

    public const string EmailRequestedRoutingKey = "notification.email.requested";
    public const string InAppRequestedRoutingKey = "notification.in-app.requested";

    public static string RoutingKeyFor(string channel) => channel switch
    {
        NotificationChannels.Email => EmailRequestedRoutingKey,
        NotificationChannels.InApp => InAppRequestedRoutingKey,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unsupported notification channel.")
    };
}

public static class NotificationChannels
{
    public const string Email = "EMAIL";
    public const string InApp = "IN_APP";
}
