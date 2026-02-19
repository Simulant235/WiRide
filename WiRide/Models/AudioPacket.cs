namespace WiRide.Models;

public class AudioPacket
{
    public string ClientId { get; set; } = string.Empty;
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public long Timestamp { get; set; }
    public int SequenceNumber { get; set; }
}
