namespace EnglishCenter.Api.Modules.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "Messaging:RabbitMq";

    public bool Enabled { get; init; }
    public bool WorkerEnabled { get; init; }
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string VirtualHost { get; init; } = "/";
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ClientProvidedName { get; init; } = "english-center-api";
    public int PublishTimeoutSeconds { get; init; } = 10;
    public int RetryDelayMilliseconds { get; init; } = 2000;
    public int MaxRetryAttempts { get; init; } = 3;
    public ushort PrefetchCount { get; init; } = 1;
}
