namespace WiRide.Models;

public enum MessageType
{
    Join,
    Leave,
    Audio,
    KeepAlive,
    Acknowledge
}

public class NetworkMessage
{
    public MessageType Type { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public long Timestamp { get; set; }
}
