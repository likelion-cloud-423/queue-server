using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LikeLionChat.Shared;

namespace LikeLionChat.Client;

public class ChatClient : IDisposable
{
    private readonly ClientWebSocket _webSocket;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly JsonSerializerOptions _serializerOptions;
    private Task? _receiveTask;

    public event EventHandler<MessageReceivePayload>? MessageReceived;
    public event EventHandler<SystemMessageReceivePayload>? SystemMessageReceived;
    public event EventHandler<ServerStatusResponsePayload>? ServerStatusReceived;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler? Disconnected;

    public bool IsConnected => _webSocket.State == WebSocketState.Open;

    public ChatClient()
    {
        _webSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task ConnectAsync(string serverUrl, string ticketId)
    {
        var uri = new Uri($"{serverUrl}?ticketId={Uri.EscapeDataString(ticketId)}");
        await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token));
    }

    public async Task SendMessageAsync(string message)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to server");
        }

        var payload = new MessageSendPayload(message);
        var envelope = new WebSocketMessage<MessageSendPayload>(WebSocketMessageTypes.MessageSend, payload);
        await SendAsync(envelope);
    }

    public async Task RequestServerStatusAsync()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Not connected to server");
        }

        var payload = new ServerStatusRequestPayload();
        var envelope = new WebSocketMessage<ServerStatusRequestPayload>(WebSocketMessageTypes.ServerStatusRequest, payload);
        await SendAsync(envelope);
    }

    private async Task SendAsync<T>(WebSocketMessage<T> message)
    {
        var json = JsonSerializer.Serialize(message, _serializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                        Disconnected?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    await HandleMessageAsync(ms, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task HandleMessageAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            var message = await JsonSerializer.DeserializeAsync<WebSocketMessage<JsonElement>>(stream, _serializerOptions, cancellationToken);
            if (message == null) return;

            switch (message.Type)
            {
                case WebSocketMessageTypes.MessageReceive:
                    var messagePayload = message.Payload.Deserialize<MessageReceivePayload>(_serializerOptions);
                    if (messagePayload != null)
                    {
                        MessageReceived?.Invoke(this, messagePayload);
                    }
                    break;

                case WebSocketMessageTypes.SystemMessageReceive:
                    var systemPayload = message.Payload.Deserialize<SystemMessageReceivePayload>(_serializerOptions);
                    if (systemPayload != null)
                    {
                        SystemMessageReceived?.Invoke(this, systemPayload);
                    }
                    break;

                case WebSocketMessageTypes.ServerStatusResponse:
                    var statusPayload = message.Payload.Deserialize<ServerStatusResponsePayload>(_serializerOptions);
                    if (statusPayload != null)
                    {
                        ServerStatusReceived?.Invoke(this, statusPayload);
                    }
                    break;
            }
        }
        catch (JsonException ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
        }

        _cancellationTokenSource.Cancel();

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _webSocket.Dispose();
    }
}
