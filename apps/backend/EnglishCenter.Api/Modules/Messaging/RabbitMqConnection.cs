using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EnglishCenter.Api.Modules.Messaging;

public sealed class RabbitMqConnection(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnection> logger) : IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection?.IsOpen == true)
        {
            return _connection;
        }

        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true)
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,
                ClientProvidedName = _options.ClientProvidedName,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            logger.LogInformation(
                "RabbitMQ connection established to {HostName}:{Port} with client name {ClientProvidedName}.",
                _options.HostName,
                _options.Port,
                _options.ClientProvidedName);
            return _connection;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionGate.Dispose();
    }
}
