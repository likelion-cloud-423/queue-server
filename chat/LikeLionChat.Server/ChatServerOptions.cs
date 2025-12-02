namespace ChatServer;

public sealed class ChatServerOptions
{
    public const string SectionName = "ChatServer";

    public long SoftCap { get; set; } = 100;
    public long MaxCap { get; set; } = 150;
}
