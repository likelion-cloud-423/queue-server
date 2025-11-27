using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatServer;

[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(WebSocketMessage<JsonElement>))]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(MessageSendPayload))]
[JsonSerializable(typeof(MessageReceivePayload))]
[JsonSerializable(typeof(ServerStatusRequestPayload))]
[JsonSerializable(typeof(ServerStatusResponsePayload))]
[JsonSerializable(typeof(SystemMessageReceivePayload))]
[JsonSerializable(typeof(WebSocketMessage<MessageReceivePayload>))]
[JsonSerializable(typeof(WebSocketMessage<MessageSendPayload>))]
[JsonSerializable(typeof(WebSocketMessage<ServerStatusRequestPayload>))]
[JsonSerializable(typeof(WebSocketMessage<ServerStatusResponsePayload>))]
[JsonSerializable(typeof(WebSocketMessage<SystemMessageReceivePayload>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
