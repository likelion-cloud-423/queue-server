using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ChatServer;

public class ChatService(ILogger<ChatService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new(StringComparer.Ordinal);
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(2);

    public int ClientCount => _clients.Count;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

    public async Task HandleClientAsync(WebSocket socket, User user, CancellationToken cancellationToken)
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
        var buffer = new byte[4 * 1024];

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            await using var ms = new MemoryStream();
            WebSocketReceiveResult? result;

            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            connection.Touch();

            var payload = Encoding.UTF8.GetString(ms.ToArray());
            var clientMessage = DeserializeClientMessage(payload);

            if (clientMessage is null)
            {
                logger.LogWarning("잘못된 메시지 형식: {Payload}", payload);
                continue;
            }

            await HandleClientMessageAsync(connection, clientMessage, cancellationToken);
        }
    }

    private async Task HandleClientMessageAsync(ClientConnection connection, WebSocketMessage<JsonElement> message, CancellationToken cancellationToken)
    {
        var type = message.Type?.Trim() ?? string.Empty;

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
        var payload = DeserializePayload(payloadElement, AppJsonSerializerContext.Default.MessageSendPayload);
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

    private static TPayload? DeserializePayload<TPayload>(JsonElement element, JsonTypeInfo<TPayload> typeInfo)
    {
        try
        {
            return element.Deserialize(typeInfo);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static WebSocketMessage<JsonElement>? DeserializeClientMessage(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize(payload, AppJsonSerializerContext.Default.WebSocketMessageJsonElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Task BroadcastMessageReceiveAsync(MessageReceivePayload message, CancellationToken cancellationToken)
    {
        var envelope = new WebSocketMessage<MessageReceivePayload>(WebSocketMessageTypes.MessageReceive, message);
        return BroadcastAsync(envelope, AppJsonSerializerContext.Default.WebSocketMessageMessageReceivePayload, cancellationToken);
    }

    private Task SendSystemMessageAsync(ClientConnection connection, string message, CancellationToken cancellationToken)
    {
        var payload = new SystemMessageReceivePayload(DateTime.UtcNow, message);
        var envelope = new WebSocketMessage<SystemMessageReceivePayload>(WebSocketMessageTypes.SystemMessageReceive, payload);
        return SendAsync(connection, envelope, AppJsonSerializerContext.Default.WebSocketMessageSystemMessageReceivePayload, cancellationToken);
    }

    private Task SendServerStatusResponseAsync(ClientConnection connection, CancellationToken cancellationToken)
    {
        var payload = new ServerStatusResponsePayload(ClientCount);
        var envelope = new WebSocketMessage<ServerStatusResponsePayload>(WebSocketMessageTypes.ServerStatusResponse, payload);
        return SendAsync(connection, envelope, AppJsonSerializerContext.Default.WebSocketMessageServerStatusResponsePayload, cancellationToken);
    }

    private Task BroadcastSystemMessageAsync(string message, CancellationToken cancellationToken)
    {
        var payload = new SystemMessageReceivePayload(DateTime.UtcNow, message);
        var envelope = new WebSocketMessage<SystemMessageReceivePayload>(WebSocketMessageTypes.SystemMessageReceive, payload);
        return BroadcastAsync(envelope, AppJsonSerializerContext.Default.WebSocketMessageSystemMessageReceivePayload, cancellationToken);
    }

    private async Task SendAsync<TPayload>(
       ClientConnection connection,
       WebSocketMessage<TPayload> message,
       JsonTypeInfo<WebSocketMessage<TPayload>> typeInfo,
       CancellationToken cancellationToken)
    {
        if (connection.Socket.State != WebSocketState.Open)
        {
            return;
        }

        var serialized = JsonSerializer.Serialize(message, typeInfo);
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
            await CloseAndRemoveAsync(userId, WebSocketCloseStatus.NormalClosure, "Idle timeout", stoppingToken);
        }
    }

    private async Task BroadcastAsync<TPayload>(WebSocketMessage<TPayload> message, JsonTypeInfo<WebSocketMessage<TPayload>> typeInfo, CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(message, typeInfo);
        var buffer = Encoding.UTF8.GetBytes(serialized);
        var segment = new ArraySegment<byte>(buffer);

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
    }

    private async Task CloseAndRemoveAsync(string userId, WebSocketCloseStatus status, string description, CancellationToken cancellationToken)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Failed to close client {ClientId}", connection.Id);
        }
        finally
        {
            connection.SendLock.Dispose();
            connection.Socket.Dispose();
            logger.LogInformation("Client {ClientId} ({UserId}) disconnected.", connection.Id, userId);
            await BroadcastSystemMessageAsync(
                message: $"{connection.User.Nickname} 님이 퇴장했습니다. 현재 접속자 {ClientCount}명",
                cancellationToken: CancellationToken.None);
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
