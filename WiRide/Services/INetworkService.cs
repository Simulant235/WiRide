using WiRide.Models;

namespace WiRide.Services;

public interface INetworkService
{
    event EventHandler<AudioPacket>? AudioPacketReceived;
    event EventHandler<ClientInfo>? ClientConnected;
    event EventHandler<string>? ClientDisconnected;
    event EventHandler<bool>? ConnectionStateChanged;
    event EventHandler<int>? LatencyUpdated;
    event EventHandler<GroupEvent>? GroupEventReceived;

    Task StartHostAsync(int port);
    Task StopHostAsync();
    Task<List<DiscoveredHost>> DiscoverHostsAsync(int port, IEnumerable<string>? extraTargets = null, int timeoutMs = 2000);
    Task<bool> ConnectToHostAsync(string hostIp, int port);
    Task DisconnectAsync();

    // Host: an clientId senden. Client: clientId ignorieren und an Host senden.
    void SendAudio(byte[] audioData, AudioCodecType codec, string? clientId = null, bool isMusic = false);

    // Host: an alle Clients (ausser exceptClientId). Client: an Host, der an alle weiterleitet.
    void SendEvent(GroupEventCode code, string riderName, string? exceptClientId = null);

    string LocalId { get; }
    bool IsHost { get; }
    bool IsConnected { get; }
    string? HostIp { get; }
    string? HostName { get; }
    int LatencyMs { get; }
    List<ClientInfo> ConnectedClients { get; }
}
