using ChatServer.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ChatServer.Repositories;

public interface ITicketRepository
{
    Task<User?> GetUserByTicketAsync(string ticketId, CancellationToken cancellationToken);
    Task ConsumeTicketAsync(string ticketId, string userId, CancellationToken cancellationToken);
}

public sealed class TicketRepository(
    ILogger<TicketRepository> logger,
    IConnectionMultiplexer connectionMultiplexer)
    : ITicketRepository
{
    private const string JoiningTicketKeyPrefix = "queue:joining:";
    private const string JoiningTicketsKey = "queue:joining:tickets";
    private const string WaitingUserKeyPrefix = "queue:waiting:user:";

    private readonly IDatabase _valkey = connectionMultiplexer.GetDatabase();

    public async Task<User?> GetUserByTicketAsync(string ticketId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var key = $"{JoiningTicketKeyPrefix}{ticketId}";

        var entries = await _valkey.HashGetAllAsync(key);
        if (entries.Length == 0)
        {
            logger.LogWarning("Ticket {TicketId} not found in Redis", ticketId);
            return null;
        }

        string? userId = null;
        string? nickname = null;

        foreach (var entry in entries)
        {
            if (userId is null && entry.Name.Equals("userId"))
            {
                userId = entry.Value.ToString();
                continue;
            }

            if (nickname is null && entry.Name.Equals("nickname"))
            {
                nickname = entry.Value.ToString();
            }
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(nickname))
        {
            logger.LogError("Ticket hash for {TicketId} is missing required fields.", ticketId);
            return null;
        }

        var ticket = new Ticket(userId, nickname);
        return new User(ticket.UserId, ticket.Nickname);
    }

    public async Task ConsumeTicketAsync(string ticketId, string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var batch = _valkey.CreateBatch();
        var deleteTicketTask = batch.KeyDeleteAsync($"{JoiningTicketKeyPrefix}{ticketId}");
        var removeFromQueueTask = batch.SortedSetRemoveAsync(JoiningTicketsKey, ticketId);
        var deleteUserMetaTask = batch.KeyDeleteAsync($"{WaitingUserKeyPrefix}{userId}");
        batch.Execute();

        try
        {
            await Task.WhenAll(deleteTicketTask, removeFromQueueTask, deleteUserMetaTask)
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clean up ticket {TicketId} for user {UserId}", ticketId, userId);
        }
    }
}

internal sealed record Ticket(string UserId, string Nickname);
