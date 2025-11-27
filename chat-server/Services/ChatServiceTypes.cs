using System;

namespace ChatServer;

public static class WebSocketMessageTypes
{
    public const string MessageSend = "MESSAGE_SEND";
    public const string MessageReceive = "MESSAGE_RECEIVE";
    public const string SystemMessageReceive = "SYSTEM_MESSAGE_RECEIVE";
    public const string ServerStatusRequest = "SERVERSTATUS_REQUEST";
    public const string ServerStatusResponse = "SERVERSTATUS_RESPONSE";
}

public sealed record WebSocketMessage<T>(string Type, T Payload);

public sealed record User(string UserId, string Nickname);

public sealed record MessageSendPayload(string Message);
public sealed record MessageReceivePayload(DateTime Timestamp, string Nickname, string Message);
public sealed record SystemMessageReceivePayload(DateTime Timestamp, string Message);

public sealed record ServerStatusRequestPayload();
public sealed record ServerStatusResponsePayload(int ClientCount);
