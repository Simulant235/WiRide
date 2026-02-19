using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using WiRide.Models;
using WiRide.Services;

namespace WiRide.Platforms.Android.Services;

public class AndroidNetworkService : INetworkService
{
    private UdpClient? _udpClient;
    private bool _isHost = false;
    private bool _isConnected = false;
    private string? _hostIp;
    private int _port = 8888;
    private readonly List<ClientInfo> _connectedClients = new();
    private CancellationTokenSource? _receiveCts;
    private readonly string _deviceId = Guid.NewGuid().ToString();

    public event EventHandler<AudioPacket>? AudioPacketReceived;
    public event EventHandler<ClientInfo>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;
    public event EventHandler<string>? HostFound;

    public bool IsHost => _isHost;
    public bool IsConnected => _isConnected;
    public string? HostIp => _hostIp;
    public List<ClientInfo> ConnectedClients => _connectedClients.ToList();

    public async Task StartHostAsync(int port = 8888)
    {
        try
        {
            _port = port;
            _udpClient = new UdpClient(port);
            _isHost = true;
            _isConnected = true;

            _receiveCts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to start host: {ex.Message}");
        }
    }

    public async Task StopHostAsync()
    {
        _isHost = false;
        _isConnected = false;
        _receiveCts?.Cancel();
        
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;

        _connectedClients.Clear();
        
        await Task.CompletedTask;
    }

    public async Task ConnectToHostAsync(string hostIp, int port = 8888)
    {
        try
        {
            _hostIp = hostIp;
            _port = port;
            _udpClient = new UdpClient();
            _udpClient.Connect(hostIp, port);

            // Send join Nachricht
            var joinMessage = new NetworkMessage
            {
                Type = MessageType.Join,
                ClientId = _deviceId,
                ClientName = $"Rider_{_deviceId[..6]}",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await SendMessageAsync(joinMessage);

            _isConnected = true;
            _receiveCts = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));

            HostFound?.Invoke(this, hostIp);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to connect to host: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (_isConnected && !_isHost && _hostIp != null)
        {
            // Send leave Nachricht
            var leaveMessage = new NetworkMessage
            {
                Type = MessageType.Leave,
                ClientId = _deviceId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await SendMessageAsync(leaveMessage);
        }

        _isConnected = false;
        _receiveCts?.Cancel();

        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
    }

    public async Task SendAudioAsync(byte[] audioData, string clientId)
    {
        if (_udpClient == null || !_isConnected)
            return;

        try
        {
            var audioPacket = new AudioPacket
            {
                ClientId = _isHost ? "host" : _deviceId,
                AudioData = audioData,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var message = new NetworkMessage
            {
                Type = MessageType.Audio,
                ClientId = audioPacket.ClientId,
                Data = audioData,
                Timestamp = audioPacket.Timestamp
            };

            var json = JsonSerializer.Serialize(message);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            if (_isHost)
            {
                // Send zu spezifischem Client
                var client = _connectedClients.FirstOrDefault(c => c.ClientId == clientId);
                if (client != null && IPAddress.TryParse(client.IpAddress, out var ipAddress))
                {
                    await _udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(ipAddress, _port));
                }
            }
            else
            {
                // Send zu host
                await _udpClient.SendAsync(bytes, bytes.Length);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending audio: {ex.Message}");
        }
    }

    private async Task SendMessageAsync(NetworkMessage message)
    {
        if (_udpClient == null)
            return;

        try
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            
            await _udpClient.SendAsync(bytes, bytes.Length);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isConnected)
        {
            try
            {
                var result = await _udpClient!.ReceiveAsync(cancellationToken);
                var json = System.Text.Encoding.UTF8.GetString(result.Buffer);
                var message = JsonSerializer.Deserialize<NetworkMessage>(json);

                if (message == null)
                    continue;

                await HandleMessageAsync(message, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Receive error: {ex.Message}");
            }
        }
    }

    private async Task HandleMessageAsync(NetworkMessage message, IPEndPoint remoteEndPoint)
    {
        switch (message.Type)
        {
            case MessageType.Join:
                if (_isHost)
                {
                    var client = new ClientInfo
                    {
                        ClientId = message.ClientId,
                        ClientName = message.ClientName,
                        IpAddress = remoteEndPoint.Address.ToString(),
                        LastSeen = DateTime.UtcNow,
                        IsConnected = true
                    };

                    _connectedClients.Add(client);
                    ClientConnected?.Invoke(this, client);

                    // Send Annerkennung
                    var ack = new NetworkMessage
                    {
                        Type = MessageType.Acknowledge,
                        ClientId = "host",
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    await SendMessageAsync(ack);
                }
                break;

            case MessageType.Leave:
                if (_isHost)
                {
                    _connectedClients.RemoveAll(c => c.ClientId == message.ClientId);
                    ClientDisconnected?.Invoke(this, message.ClientId);
                }
                break;

            case MessageType.Audio:
                var audioPacket = new AudioPacket
                {
                    ClientId = message.ClientId,
                    AudioData = message.Data,
                    Timestamp = message.Timestamp
                };
                AudioPacketReceived?.Invoke(this, audioPacket);
                break;
        }

        await Task.CompletedTask;
    }
}
