using ChatServer;
using StackExchange.Redis;
using System.Diagnostics;
using System.Net;

var builder = WebApplication.CreateSlimBuilder(args);

builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string is not configured.");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<ITicketRepository, RedisTicketRepository>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ChatService>());
builder.Services.AddSingleton<AuthService>();

var app = builder.Build();

app.MapDefaultEndpoints();

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};

app.UseWebSockets(webSocketOptions);

var group = app.MapGroup("/gameserver");

group.Map("/", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }

    var ticketId = context.Request.Query["ticketId"].ToString();
    var authService = context.RequestServices.GetRequiredService<AuthService>();
    var chatService = context.RequestServices.GetRequiredService<ChatService>();
    var authResult = await authService.AuthenticateAsync(ticketId, context.RequestAborted);

    if (!authResult.IsAuthenticated)
    {
        var errorResult = Results.Json(
            authResult.Error,
            AppJsonSerializerContext.Default.ErrorResponse,
            statusCode: authResult.StatusCode);
        await errorResult.ExecuteAsync(context);
        return;
    }

    Debug.Assert(authResult.User is not null);

    if (chatService.IsUserConnected(authResult.User.UserId))
    {
        var duplicateResult = Results.Json(
            new ErrorResponse("DuplicateConnection", "User already has an active session."),
            AppJsonSerializerContext.Default.ErrorResponse,
            statusCode: StatusCodes.Status409Conflict);
        await duplicateResult.ExecuteAsync(context);
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await chatService.HandleClientAsync(socket, authResult.User, context.RequestAborted);
});

// TODO: TelemetryApi, TelemetryService 로 옮기기
group.MapGet("/clients", (ChatService chatService) =>
    Results.Ok(new { Count = chatService.ClientCount }));

app.MapGet("/", () => "Chat Server is running.");

app.Run();
