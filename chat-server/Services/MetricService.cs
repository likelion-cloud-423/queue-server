using System.Diagnostics.Metrics;

namespace ChatServer.Services;

public sealed class MetricService : IDisposable
{
    public const string MeterName = "ChatServer";

    private readonly Meter _meter;
    private readonly ObservableGauge<int> _currentUsersGauge;
    private readonly Counter<long> _connectionsTotal;
    private readonly Counter<long> _disconnectionsTotal;
    private readonly Counter<long> _messagesReceivedTotal;
    private readonly Counter<long> _messagesSentTotal;
    private readonly Counter<long> _messagesBroadcastTotal;
    private readonly Counter<long> _authFailuresTotal;
    private readonly Counter<long> _idleDisconnectsTotal;
    private readonly Histogram<double> _messageSizeBytes;

    private int _currentUsers;

    public MetricService(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _currentUsersGauge = _meter.CreateObservableGauge(
            name: "chatserver.current_users",
            observeValue: () => _currentUsers,
            unit: "{users}",
            description: "Current number of connected users");

        _connectionsTotal = _meter.CreateCounter<long>(
            name: "chatserver.connections_total",
            unit: "{connections}",
            description: "Total number of user connections");

        _disconnectionsTotal = _meter.CreateCounter<long>(
            name: "chatserver.disconnections_total",
            unit: "{disconnections}",
            description: "Total number of user disconnections");

        _messagesReceivedTotal = _meter.CreateCounter<long>(
            name: "chatserver.messages_received_total",
            unit: "{messages}",
            description: "Total number of messages received from clients");

        _messagesSentTotal = _meter.CreateCounter<long>(
            name: "chatserver.messages_sent_total",
            unit: "{messages}",
            description: "Total number of messages sent to clients");

        _messagesBroadcastTotal = _meter.CreateCounter<long>(
            name: "chatserver.messages_broadcast_total",
            unit: "{messages}",
            description: "Total number of broadcast messages");

        _authFailuresTotal = _meter.CreateCounter<long>(
            name: "chatserver.auth_failures_total",
            unit: "{failures}",
            description: "Total number of authentication failures");

        _idleDisconnectsTotal = _meter.CreateCounter<long>(
            name: "chatserver.idle_disconnects_total",
            unit: "{disconnections}",
            description: "Total number of idle timeout disconnections");

        _messageSizeBytes = _meter.CreateHistogram<double>(
            name: "chatserver.message_size_bytes",
            unit: "By",
            description: "Size of received messages in bytes");
    }

    public void SetCurrentUsers(int count)
    {
        _currentUsers = count;
    }

    public void RecordConnection()
    {
        _connectionsTotal.Add(1);
    }

    public void RecordDisconnection()
    {
        _disconnectionsTotal.Add(1);
    }

    public void RecordMessageReceived(int sizeBytes)
    {
        _messagesReceivedTotal.Add(1);
        _messageSizeBytes.Record(sizeBytes);
    }

    public void RecordMessageSent()
    {
        _messagesSentTotal.Add(1);
    }

    public void RecordBroadcast(int recipientCount)
    {
        _messagesBroadcastTotal.Add(1);
        _messagesSentTotal.Add(recipientCount);
    }

    public void RecordAuthFailure()
    {
        _authFailuresTotal.Add(1);
    }

    public void RecordIdleDisconnect()
    {
        _idleDisconnectsTotal.Add(1);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
