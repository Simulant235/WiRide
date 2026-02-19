using WiRide.Models;

namespace WiRide.Services;

public interface INetworkService
{
    event EventHandler<AudioPacket>? AudioPacketReceived;
    event EventHandler<ClientInfo>? ClientConnected;
    event EventHandler<string>? ClientDisconnected;
    event EventHandler<string>? HostFound;
    
    Task StartHostAsync(int port = 8888);
    Task StopHostAsync();
    Task ConnectToHostAsync(string hostIp, int port = 8888);
    Task DisconnectAsync();
    Task SendAudioAsync(byte[] audioData, string clientId);
    
    bool IsHost { get; }
    bool IsConnected { get; }
    string? HostIp { get; }
    List<ClientInfo> ConnectedClients { get; }
}
