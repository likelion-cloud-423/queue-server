using ChatServer.Repositories;
using Microsoft.AspNetCore.Http;

namespace ChatServer.Services;

public sealed record User(string UserId, string Nickname);

public sealed class AuthService(ITicketRepository ticketRepository, MetricService metrics)
{
    public async Task<AuthenticationResult> AuthenticateAsync(string? ticketId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            metrics.RecordAuthFailure();
            return AuthenticationResult.Fail(StatusCodes.Status401Unauthorized, "MissingTicketId", "ticketId query parameter is required.");
        }

        var user = await ticketRepository.GetUserByTicketAsync(ticketId, cancellationToken);
        if (user is null)
        {
            metrics.RecordAuthFailure();
            return AuthenticationResult.Fail(StatusCodes.Status401Unauthorized, "InvalidTicket", "ticketId is invalid or expired.");
        }

        return AuthenticationResult.Success(user);
    }
}

public sealed record AuthenticationResult(
    bool IsAuthenticated,
    User? User,
    int StatusCode,
    ErrorResponse? Error)
{
    public static AuthenticationResult Success(User user)
    {
        return new(true, user, StatusCodes.Status200OK, null);
    }

    public static AuthenticationResult Fail(int statusCode, string code, string message)
    {
        return new(false, null, statusCode, new ErrorResponse(code, message));
    }
}
