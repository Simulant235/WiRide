namespace WiRide.Models;

public enum MessageType : byte
{
    Discover = 1,   // Client -> Broadcast: "Gibt es einen Host?"
    Announce,       // Host -> Client: Antwort auf Discover
    Join,           // Client -> Host: Beitreten
    Accept,         // Host -> Client: Beitritt bestaetigt
    Leave,          // Client -> Host: Verlassen
    Audio,          // Audio-Frame (beide Richtungen)
    Ping,           // Client -> Host: KeepAlive + Latenzmessung
    Pong,           // Host -> Client: Antwort auf Ping
    Event,          // Ereignis/Schnell-Ansage (Client -> Host -> alle)
    Music           // Geteilte Musik (wie Audio, eigener Kanal)
}

// Sprachunabhaengige Ereignis-Codes; jedes Handy sagt sie in seiner eigenen Sprache an
public enum GroupEventCode : byte
{
    RiderJoined = 1,
    RiderLeft = 2,
    Pause = 10,
    Fuel = 11,
    Police = 12,
    Danger = 13,
    SlowDown = 14,
    TurnLeft = 15,
    TurnRight = 16,
    WaitForMe = 17,
    AllGood = 18
}

public enum AudioCodecType : byte
{
    Pcm16 = 0,
    Opus = 1
}

// Kompaktes Binaer-Protokoll (statt JSON), damit Audio-Pakete klein bleiben.
// Header: 'W' 'R' Version Typ SenderId(16 Bytes) = 20 Bytes
public class NetworkMessage
{
    private const byte Magic1 = (byte)'W';
    private const byte Magic2 = (byte)'R';
    public const byte ProtocolVersion = 1;
    private const int HeaderSize = 20;

    public MessageType Type { get; set; }
    public Guid SenderId { get; set; }

    // Discover / Announce / Join / Accept
    public string Name { get; set; } = string.Empty;

    // Audio
    public int Sequence { get; set; }
    public AudioCodecType Codec { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();

    // Ping / Pong: Zeitstempel des Clients + zuletzt gemessene Latenz
    public long Timestamp { get; set; }
    public int LatencyMs { get; set; }

    // Ping-Zusatzinfos (optional, aeltere Versionen senden sie nicht)
    // Event
    public GroupEventCode EventCode { get; set; }

    public byte NormalizeStrength { get; set; }   // 0 = aus, 1-100 = Staerke der Normalisierung fuer den eigenen Mix
    public sbyte BatteryPercent { get; set; } = -1; // -1 = unbekannt
    public short SignalDbm { get; set; }            // 0 = unbekannt

    public byte[] Serialize()
    {
        using var stream = new MemoryStream(HeaderSize + Data.Length + 16);
        using var writer = new BinaryWriter(stream);

        writer.Write(Magic1);
        writer.Write(Magic2);
        writer.Write(ProtocolVersion);
        writer.Write((byte)Type);
        writer.Write(SenderId.ToByteArray());

        switch (Type)
        {
            case MessageType.Discover:
            case MessageType.Announce:
            case MessageType.Join:
            case MessageType.Accept:
                writer.Write(Name);
                break;

            case MessageType.Event:
                writer.Write((byte)EventCode);
                writer.Write(Name);
                break;

            case MessageType.Audio:
            case MessageType.Music:
                writer.Write(Sequence);
                writer.Write((byte)Codec);
                writer.Write(Data);
                break;

            case MessageType.Ping:
            case MessageType.Pong:
                writer.Write(Timestamp);
                writer.Write(LatencyMs);
                if (Type == MessageType.Ping)
                {
                    writer.Write(NormalizeStrength);
                    writer.Write(BatteryPercent);
                    writer.Write(SignalDbm);
                }
                break;
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static NetworkMessage? Parse(byte[] buffer)
    {
        if (buffer.Length < HeaderSize || buffer[0] != Magic1 || buffer[1] != Magic2 || buffer[2] != ProtocolVersion)
            return null;

        try
        {
            using var reader = new BinaryReader(new MemoryStream(buffer));
            reader.ReadBytes(3);

            var message = new NetworkMessage
            {
                Type = (MessageType)reader.ReadByte(),
                SenderId = new Guid(reader.ReadBytes(16))
            };

            switch (message.Type)
            {
                case MessageType.Discover:
                case MessageType.Announce:
                case MessageType.Join:
                case MessageType.Accept:
                    message.Name = reader.ReadString();
                    break;

                case MessageType.Event:
                    message.EventCode = (GroupEventCode)reader.ReadByte();
                    message.Name = reader.ReadString();
                    break;

                case MessageType.Audio:
                case MessageType.Music:
                    message.Sequence = reader.ReadInt32();
                    message.Codec = (AudioCodecType)reader.ReadByte();
                    message.Data = reader.ReadBytes(buffer.Length - HeaderSize - 5);
                    break;

                case MessageType.Ping:
                case MessageType.Pong:
                    message.Timestamp = reader.ReadInt64();
                    message.LatencyMs = reader.ReadInt32();
                    if (message.Type == MessageType.Ping && reader.BaseStream.Length - reader.BaseStream.Position >= 4)
                    {
                        message.NormalizeStrength = reader.ReadByte();
                        message.BatteryPercent = reader.ReadSByte();
                        message.SignalDbm = reader.ReadInt16();
                    }
                    break;

                case MessageType.Leave:
                    break;

                default:
                    return null;
            }

            return message;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }
}
