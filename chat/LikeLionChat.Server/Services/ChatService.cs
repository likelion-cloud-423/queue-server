using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChatServer.Repositories;
using LikeLionChat.Shared;

namespace ChatServer.Services;

public class ChatService(
    ILogger<ChatService> logger,
    ITicketRepository ticketRepository,
    IServerStatusService serverStatusService,
    MetricService metrics) : BackgroundService
{
    private const int ReceiveBufferSize = 4 * 1024;
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new(StringComparer.Ordinal);
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(2);

    public int ClientCount => _clients.Count;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await UpdateServerStatusAsync(stoppingToken);

        // Periodically scan for idle clients and close their sockets.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckIdleClientsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ChatServer background loop failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down.
                break;
            }
        }
    }

    public async Task HandleClientAsync(WebSocket socket, User user, string ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var clientId = Guid.NewGuid();
        var connection = new ClientConnection(clientId, socket, user);

        if (!_clients.TryAdd(user.UserId, connection))
        {
            await socket.CloseAsync(WebSocketCloseStatus.InternalServerError,
                "Failed to register connection", cancellationToken);
            return;
        }

        logger.LogInformation("Client {ClientId} ({UserId}) connected.", clientId, user.UserId);

        metrics.RecordConnection();
        await ticketRepository.ConsumeTicketAsync(ticketId, user.UserId, cancellationToken);
        await UpdateServerStatusAsync(cancellationToken);

        await BroadcastSystemMessageAsync(
            message: $"{connection.User.Nickname} 님이 입장했습니다.",
            cancellationToken: cancellationToken);

        try
        {
            await ReceiveLoopAsync(connection, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Host cancelled the connection.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Receive loop crashed for client {ClientId}", clientId);
        }
        finally
        {
            await CloseAndRemoveAsync(user.UserId, WebSocketCloseStatus.NormalClosure,
                "Disconnected", CancellationToken.None);
        }
    }

    public bool IsUserConnected(string userId)
    {
        return _clients.ContainsKey(userId);
    }


    private async Task ReceiveLoopAsync(ClientConnection connection, CancellationToken cancellationToken)
    {
        var socket = connection.Socket;
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);
        var messageBuffer = new ArrayBufferWriter<byte>(ReceiveBufferSize);

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                messageBuffer.Clear();
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    if (result.Count > 0)
                    {
                        messageBuffer.Write(buffer.AsSpan(0, result.Count));
                    }
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                connection.Touch();

                var payloadSpan = messageBuffer.WrittenSpan;
                metrics.RecordMessageReceived(payloadSpan.Length);

                string? payloadText = null;
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    payloadText = Encoding.UTF8.GetString(payloadSpan);
                    logger.LogDebug("{Payload}", payloadText);
                }

                var clientMessage = DeserializeClientMessage(payloadSpan);

                if (clientMessage is null)
                {
                    payloadText ??= Encoding.UTF8.GetString(payloadSpan);
                    logger.LogWarning("잘못된 메시지 형식: {Payload}", payloadText);
                    continue;
                }

                await HandleClientMessageAsync(connection, clientMessage, cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task HandleClientMessageAsync(ClientConnection connection, WebSocketMessage<JsonElement> message,
        CancellationToken cancellationToken)
    {
        var type = message.Type.Trim();

        switch (type)
        {
            case WebSocketMessageTypes.MessageSend:
                await HandleMessageSendAsync(connection, message.Payload, cancellationToken);
                break;
            case WebSocketMessageTypes.ServerStatusRequest:
                await SendServerStatusResponseAsync(connection, cancellationToken);
                break;
            default:
                logger.LogWarning("Unhandled message type {Type}", type);
                break;
        }
    }

    private async Task HandleMessageSendAsync(ClientConnection connection, JsonElement payloadElement, CancellationToken cancellationToken)
    {
        var payload = DeserializePayload<MessageSendPayload>(payloadElement);
        if (payload is null || string.IsNullOrWhiteSpace(payload.Message))
        {
            return;
        }

        var outbound = new MessageReceivePayload(
            Timestamp: DateTime.UtcNow,
            Nickname: connection.User.Nickname,
            Message: payload.Message.Trim());

        await BroadcastMessageReceiveAsync(outbound, cancellationToken);
    }

    private static TPayload? DeserializePayload<TPayload>(JsonElement element)
    {
        try
        {
            return element.Deserialize<TPayload>(s_serializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static WebSocketMessage<JsonElement>? DeserializeClientMessage(ReadOnlySpan<byte> payload)
    {
        try
        {
            return JsonSerializer.Deserialize<WebSocketMessage<JsonElement>>(payload, s_serializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Task BroadcastMessageReceiveAsync(MessageReceivePayload message, CancellationToken cancellationToken)
    {
        var envelope = new WebSocketMessage<MessageReceivePayload>(WebSocketMessageTypes.MessageReceive, message);
        return BroadcastAsync(envelope, cancellationToken);
    }

    private Task SendSystemMessageAsync(ClientConnection connection, string message, CancellationToken cancellationToken)
    {
        var payload = new SystemMessageReceivePayload(DateTime.UtcNow, message);
        var envelope = new WebSocketMessage<SystemMessageReceivePayload>(WebSocketMessageTypes.SystemMessageReceive, payload);
        return SendAsync(connection, envelope, cancellationToken);
    }

    private Task SendServerStatusResponseAsync(ClientConnection connection, CancellationToken cancellationToken)
    {
        var payload = new ServerStatusResponsePayload(ClientCount);
        var envelope = new WebSocketMessage<ServerStatusResponsePayload>(WebSocketMessageTypes.ServerStatusResponse, payload);
        return SendAsync(connection, envelope, cancellationToken);
    }

    private Task BroadcastSystemMessageAsync(string message, CancellationToken cancellationToken)
    {
        var payload = new SystemMessageReceivePayload(DateTime.UtcNow, message);
        var envelope = new WebSocketMessage<SystemMessageReceivePayload>(WebSocketMessageTypes.SystemMessageReceive, payload);
        return BroadcastAsync(envelope, cancellationToken);
    }

    private async Task SendAsync<TPayload>(
        ClientConnection connection,
        WebSocketMessage<TPayload> message,
        CancellationToken cancellationToken)
    {
        if (connection.Socket.State != WebSocketState.Open)
        {
            return;
        }

        var serialized = JsonSerializer.Serialize(message, s_serializerOptions);
        var buffer = Encoding.UTF8.GetBytes(serialized);
        var segment = new ArraySegment<byte>(buffer);

        var lockHeld = false;
        try
        {
            await connection.SendLock.WaitAsync(cancellationToken);
            lockHeld = true;
            await connection.Socket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to send message to client {ClientId}", connection.Id);
            await CloseAndRemoveAsync(connection.UserId, WebSocketCloseStatus.InternalServerError, "Send failure",
                cancellationToken);
        }
        finally
        {
            if (lockHeld)
            {
                connection.SendLock.Release();
            }
        }
    }

    private async Task CheckIdleClientsAsync(CancellationToken stoppingToken)
    {
        foreach (var (userId, connection) in _clients)
        {
            if (DateTime.UtcNow - connection.LastSeenUtc < _idleTimeout)
            {
                continue;
            }

            logger.LogInformation("Disconnecting idle client {ClientId} ({UserId})", connection.Id, userId);
            metrics.RecordIdleDisconnect();
            await CloseAndRemoveAsync(userId, WebSocketCloseStatus.NormalClosure, "Idle timeout", stoppingToken);
        }
    }

    private async Task BroadcastAsync<TPayload>(WebSocketMessage<TPayload> message, CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(message, s_serializerOptions);
        var buffer = Encoding.UTF8.GetBytes(serialized);
        var segment = new ArraySegment<byte>(buffer);

        var recipientCount = 0;
        foreach (var connection in _clients.Values)
        {
            if (connection.Socket.State != WebSocketState.Open)
            {
                continue;
            }

            var lockHeld = false;
            try
            {
                await connection.SendLock.WaitAsync(cancellationToken);
                lockHeld = true;
                await connection.Socket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
                recipientCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to broadcast to client {ClientId}", connection.Id);
                await CloseAndRemoveAsync(connection.UserId, WebSocketCloseStatus.InternalServerError,
                    "Broadcast failure", cancellationToken);
            }
            finally
            {
                if (lockHeld)
                {
                    connection.SendLock.Release();
                }
            }
        }

        metrics.RecordBroadcast(recipientCount);
    }

    private async Task CloseAndRemoveAsync(string userId, WebSocketCloseStatus status, string description,
        CancellationToken cancellationToken)
    {
        if (!_clients.TryRemove(userId, out var connection))
        {
            return;
        }

        try
        {
            if (connection.Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await connection.Socket.CloseAsync(status, description, cancellationToken);
            }
        }
        catch (ObjectDisposedException)
        {
            // Socket already disposed by another operation
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Failed to close client {ClientId}", connection.Id);
        }
        finally
        {
            try
            {
                connection.SendLock.Dispose();
            }
            catch (ObjectDisposedException) { }
            
            try
            {
                connection.Socket.Dispose();
            }
            catch (ObjectDisposedException) { }
            
            logger.LogInformation("Client {ClientId} ({UserId}) disconnected.", connection.Id, userId);
            metrics.RecordDisconnection();
            await UpdateServerStatusAsync(CancellationToken.None);
            await BroadcastSystemMessageAsync(message: $"{connection.User.Nickname} 님이 퇴장했습니다.", cancellationToken: CancellationToken.None);
        }
    }

    private async Task UpdateServerStatusAsync(CancellationToken cancellationToken)
    {
        var currentCount = ClientCount;
        metrics.SetCurrentUsers(currentCount);

        try
        {
            await serverStatusService.PublishAsync(currentCount, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish server status snapshot.");
        }
    }

    private sealed class ClientConnection(Guid id, WebSocket socket, User user)
    {
        public Guid Id { get; } = id;
        public WebSocket Socket { get; } = socket;
        public User User { get; } = user;
        public string UserId { get; } = user.UserId;
        public DateTime LastSeenUtc { get; private set; } = DateTime.UtcNow;
        public SemaphoreSlim SendLock { get; } = new(1, 1);

        public void Touch() => LastSeenUtc = DateTime.UtcNow;
    }
}
