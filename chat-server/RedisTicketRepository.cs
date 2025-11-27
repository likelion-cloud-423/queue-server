using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ChatServer;

public interface ITicketRepository
{
    Task<User?> GetUserByTicketAsync(string ticketId, CancellationToken cancellationToken);
}

public sealed class RedisTicketRepository(
    ILogger<RedisTicketRepository> logger,
    IConnectionMultiplexer connectionMultiplexer)
    : ITicketRepository
{
    private const string TicketKeyPrefix = "queue:granted:";

    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();

    public async Task<User?> GetUserByTicketAsync(string ticketId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested(); 

        var redisKey = $"{TicketKeyPrefix}{ticketId}";
        var redisValue = await _redis.StringGetAsync(redisKey);
        if (redisValue.IsNullOrEmpty)
        {
            logger.LogWarning("Ticket {TicketId} not found in Redis", ticketId);
            return null;
        }

        var user = JsonSerializer.Deserialize<User>(redisValue.ToString(), AppJsonSerializerContext.Default.User);
        if (user is not null)
        {
            return user;
        }

        logger.LogError("Failed to deserialize user for ticket {TicketId}", ticketId);
        return null;
    }
}