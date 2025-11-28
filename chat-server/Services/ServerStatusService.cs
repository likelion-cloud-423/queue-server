using System.Collections.Generic;
using ChatServer;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ChatServer.Services;

public interface IServerStatusService
{
    Task PublishAsync(int currentUsers, CancellationToken cancellationToken);
}

public sealed class ServerStatusService(
    ILogger<ServerStatusService> logger,
    IOptionsMonitor<ChatServerOptions> optionsMonitor,
    IConnectionMultiplexer connectionMultiplexer) : IServerStatusService
{
    private const string ServerStatusKey = "server:status";

    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();
    private long? _lastSoftCap;
    private long? _lastMaxCap;

    public async Task PublishAsync(int currentUsers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var options = optionsMonitor.CurrentValue;
            var softCap = options.SoftCap > 0 ? options.SoftCap : Math.Max(options.MaxCap, 1);
            var maxCap = options.MaxCap > 0 ? options.MaxCap : softCap;

            var publishTasks = new List<Task>
            {
                _redis.HashSetAsync(ServerStatusKey, "current_users", currentUsers)
            };

            if (_lastSoftCap != softCap || _lastMaxCap != maxCap)
            {
                var capacityFields = new HashEntry[]
                {
                    new("soft_cap", softCap),
                    new("max_cap", maxCap)
                };
                publishTasks.Add(_redis.HashSetAsync(ServerStatusKey, capacityFields));
                _lastSoftCap = softCap;
                _lastMaxCap = maxCap;
            }

            await Task.WhenAll(publishTasks);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish server status to Redis.");
            throw;
        }
    }
}
