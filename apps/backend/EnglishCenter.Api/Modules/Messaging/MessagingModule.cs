using Microsoft.Extensions.Options;

namespace EnglishCenter.Api.Modules.Messaging;

public static class MessagingModule
{
    public static IServiceCollection AddMessagingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(RabbitMqOptions.SectionName);
        var options = section.Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        services.Configure<RabbitMqOptions>(section);

        if (!options.Enabled)
        {
            services.AddSingleton<IIntegrationEventPublisher, DisabledIntegrationEventPublisher>();
            return services;
        }

        ValidateOptions(options);
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();
        services.AddHostedService<RabbitMqTopologyInitializer>();
        services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>("rabbitmq");
        return services;
    }

    private static void ValidateOptions(RabbitMqOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HostName) ||
            string.IsNullOrWhiteSpace(options.UserName) ||
            string.IsNullOrWhiteSpace(options.Password) ||
            string.IsNullOrWhiteSpace(options.VirtualHost))
        {
            throw new OptionsValidationException(
                RabbitMqOptions.SectionName,
                typeof(RabbitMqOptions),
                ["RabbitMQ host, virtual host, username and password are required when messaging is enabled."]);
        }

        if (options.Port is < 1 or > 65535 || options.PublishTimeoutSeconds is < 1 or > 60)
        {
            throw new OptionsValidationException(
                RabbitMqOptions.SectionName,
                typeof(RabbitMqOptions),
                ["RabbitMQ port or publish timeout is outside the supported range."]);
        }
    }
}
