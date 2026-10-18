using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using WiRide.Models;

namespace WiRide.Services;

// Plattformunabhaengiger UDP-Service (laeuft auf Android und Windows).
// Host: lauscht auf festem Port, verwaltet Clients, beantwortet Discover/Ping.
// Client: verbindet sich mit Host, sendet KeepAlive-Pings und verbindet bei Abbruch automatisch neu.
public class UdpNetworkService : INetworkService
{
    private const int KeepAliveIntervalMs = 1000;
    private const int TimeoutMs = 5000;

    private readonly Guid _localId = Guid.NewGuid();
    private readonly ConcurrentDictionary<string, ClientInfo> _clients = new();

    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private IPEndPoint? _hostEndPoint;
    private TaskCompletionSource<bool>? _joinTcs;
    private long _lastHostContact;
    private int _sequence;
    private int _latencyMs;
    private bool _isHost;
    private bool _isConnected;
    private string? _hostName;

#if ANDROID
    private global::Android.Net.Wifi.WifiManager.MulticastLock? _multicastLock;
#endif

    public event EventHandler<AudioPacket>? AudioPacketReceived;
    public event EventHandler<ClientInfo>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;
    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<int>? LatencyUpdated;
    public event EventHandler<GroupEvent>? GroupEventReceived;

    public string LocalId => _localId.ToString("N");
    public bool IsHost => _isHost;
    public bool IsConnected => _isConnected;
    public string? HostIp => _hostEndPoint?.Address.ToString();
    public string? HostName => _hostName;
    public int LatencyMs => _latencyMs;
    public List<ClientInfo> ConnectedClients => _clients.Values.ToList();

    private static long Now => Environment.TickCount64;

    // ---------------- Host ----------------

    public Task StartHostAsync(int port)
    {
        if (_udp != null)
            throw new InvalidOperationException("Netzwerk laeuft bereits");

        var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        udp.EnableBroadcast = true;
        IgnoreConnectionReset(udp);

        _udp = udp;
        _isHost = true;
        _isConnected = true;
        _clients.Clear();
        AcquireMulticastLock();

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoopAsync(udp, _cts.Token));
        _ = Task.Run(() => HostMaintenanceLoopAsync(_cts.Token));

        return Task.CompletedTask;
    }

    public Task StopHostAsync()
    {
        Shutdown();
        return Task.CompletedTask;
    }

    private async Task HostMaintenanceLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(KeepAliveIntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Clients ohne Lebenszeichen entfernen
            foreach (var client in _clients.Values)
            {
                if ((DateTime.UtcNow - client.LastSeen).TotalMilliseconds > TimeoutMs)
                    RemoveClient(client.ClientId);
            }
        }
    }

    private void RemoveClient(string clientId)
    {
        if (_clients.TryRemove(clientId, out _))
            ClientDisconnected?.Invoke(this, clientId);
    }

    // ---------------- Client ----------------

    public async Task<List<DiscoveredHost>> DiscoverHostsAsync(int port, IEnumerable<string>? extraTargets = null, int timeoutMs = 2000)
    {
        var found = new Dictionary<string, DiscoveredHost>();
        AcquireMulticastLock();

        using var udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        IgnoreConnectionReset(udp);

        var discover = new NetworkMessage
        {
            Type = MessageType.Discover,
            SenderId = _localId,
            Name = AppSettings.RiderName
        }.Serialize();

        var targets = new List<IPAddress> { IPAddress.Broadcast };
        foreach (var target in extraTargets ?? Enumerable.Empty<string>())
        {
            if (IPAddress.TryParse(target, out var ip))
                targets.Add(ip);
        }

        using var cts = new CancellationTokenSource(timeoutMs);

        // Discover mehrmals senden (UDP kann verloren gehen)
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                foreach (var ip in targets)
                {
                    try { udp.Send(discover, discover.Length, new IPEndPoint(ip, port)); }
                    catch (Exception ex) { Debug.WriteLine($"Discover an {ip} fehlgeschlagen: {ex.Message}"); }
                }
                try { await Task.Delay(400, cts.Token); } catch { break; }
            }
        });

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(cts.Token);
                var message = NetworkMessage.Parse(result.Buffer);
                if (message?.Type == MessageType.Announce)
                {
                    var ip = result.RemoteEndPoint.Address.ToString();
                    found.TryAdd(message.SenderId.ToString("N"), new DiscoveredHost { Name = message.Name, IpAddress = ip });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Discover-Empfang fehlgeschlagen: {ex.Message}");
            }
        }

        if (!_isHost)
            ReleaseMulticastLock();

        return found.Values.ToList();
    }

    public async Task<bool> ConnectToHostAsync(string hostIp, int port)
    {
        if (_udp != null)
            Shutdown();

        if (!IPAddress.TryParse(hostIp, out var address))
            return false;

        var udp = new UdpClient(AddressFamily.InterNetwork);
        IgnoreConnectionReset(udp);

        _udp = udp;
        _isHost = false;
        _hostEndPoint = new IPEndPoint(address, port);
        _joinTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoopAsync(udp, _cts.Token));

        // Join mehrmals senden, bis Host bestaetigt
        for (int attempt = 0; attempt < 6 && !_joinTcs.Task.IsCompleted; attempt++)
        {
            SendJoin();
            await Task.WhenAny(_joinTcs.Task, Task.Delay(500));
        }

        if (!_joinTcs.Task.IsCompleted)
        {
            Shutdown();
            return false;
        }

        _ = Task.Run(() => ClientKeepAliveLoopAsync(_cts.Token));
        return true;
    }

    public Task DisconnectAsync()
    {
        if (!_isHost && _hostEndPoint != null)
            Send(new NetworkMessage { Type = MessageType.Leave, SenderId = _localId }, _hostEndPoint);

        Shutdown();
        return Task.CompletedTask;
    }

    private void SendJoin()
    {
        if (_hostEndPoint != null)
            Send(new NetworkMessage { Type = MessageType.Join, SenderId = _localId, Name = AppSettings.RiderName }, _hostEndPoint);
    }

    private async Task ClientKeepAliveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(KeepAliveIntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_hostEndPoint == null)
                continue;

            bool hostAlive = Now - Interlocked.Read(ref _lastHostContact) < TimeoutMs;

            if (_isConnected && !hostAlive)
            {
                // Verbindung verloren -> ab jetzt automatisch neu verbinden
                SetConnected(false);
            }

            if (!_isConnected)
            {
                if (AppSettings.AutoReconnect)
                    SendJoin();
                continue;
            }

            Send(new NetworkMessage
            {
                Type = MessageType.Ping,
                SenderId = _localId,
                Timestamp = Now,
                LatencyMs = _latencyMs,
                NormalizeStrength = AppSettings.NormalizeEnabled ? (byte)Math.Clamp(AppSettings.NormalizeStrength * 100, 1, 100) : (byte)0,
                BatteryPercent = DeviceStatus.BatteryPercent,
                SignalDbm = DeviceStatus.SignalDbm
            }, _hostEndPoint);
        }
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected == connected)
            return;

        _isConnected = connected;
        ConnectionStateChanged?.Invoke(this, connected);
    }

    // ---------------- Gemeinsam ----------------

    public void SendEvent(GroupEventCode code, string riderName, string? exceptClientId = null)
    {
        if (!_isConnected)
            return;

        var message = new NetworkMessage { Type = MessageType.Event, SenderId = _localId, EventCode = code, Name = riderName };

        if (!_isHost)
        {
            if (_hostEndPoint != null)
                Send(message, _hostEndPoint);
            return;
        }

        foreach (var client in _clients.Values)
        {
            if (client.ClientId != exceptClientId && client.EndPoint != null)
                Send(message, client.EndPoint);
        }
    }

    public void SendAudio(byte[] audioData, AudioCodecType codec, string? clientId = null, bool isMusic = false)
    {
        if (!_isConnected)
            return;

        IPEndPoint? target;
        if (_isHost)
        {
            if (clientId == null || !_clients.TryGetValue(clientId, out var client))
                return;
            target = client.EndPoint;
        }
        else
        {
            target = _hostEndPoint;
        }

        if (target == null)
            return;

        Send(new NetworkMessage
        {
            Type = isMusic ? MessageType.Music : MessageType.Audio,
            SenderId = _localId,
            Sequence = Interlocked.Increment(ref _sequence),
            Codec = codec,
            Data = audioData
        }, target);
    }

    private void Send(NetworkMessage message, IPEndPoint target)
    {
        var udp = _udp;
        if (udp == null)
            return;

        try
        {
            var bytes = message.Serialize();
            udp.Send(bytes, bytes.Length, target);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Senden fehlgeschlagen ({message.Type}): {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(UdpClient udp, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(token);
                var message = NetworkMessage.Parse(result.Buffer);
                if (message == null || message.SenderId == _localId)
                    continue;

                if (_isHost)
                    HandleHostMessage(message, result.RemoteEndPoint);
                else
                    HandleClientMessage(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Empfangsfehler: {ex.Message}");
            }
        }
    }

    private void HandleHostMessage(NetworkMessage message, IPEndPoint remote)
    {
        var clientId = message.SenderId.ToString("N");

        switch (message.Type)
        {
            case MessageType.Discover:
                Send(new NetworkMessage { Type = MessageType.Announce, SenderId = _localId, Name = AppSettings.RiderName }, remote);
                break;

            case MessageType.Join:
                var isNew = !_clients.TryGetValue(clientId, out var client);
                client ??= new ClientInfo { ClientId = clientId };
                client.ClientName = message.Name;
                client.IpAddress = remote.Address.ToString();
                client.EndPoint = remote;
                client.LastSeen = DateTime.UtcNow;
                client.IsConnected = true;
                _clients[clientId] = client;

                Send(new NetworkMessage { Type = MessageType.Accept, SenderId = _localId, Name = AppSettings.RiderName }, remote);

                if (isNew)
                    ClientConnected?.Invoke(this, client);
                break;

            case MessageType.Leave:
                RemoveClient(clientId);
                break;

            case MessageType.Ping:
                if (_clients.TryGetValue(clientId, out var pinger))
                {
                    pinger.LastSeen = DateTime.UtcNow;
                    pinger.EndPoint = remote;
                    pinger.LatencyMs = message.LatencyMs;
                    pinger.NormalizeStrength = message.NormalizeStrength;
                    pinger.BatteryPercent = message.BatteryPercent;
                    pinger.SignalDbm = message.SignalDbm;
                    Send(new NetworkMessage { Type = MessageType.Pong, SenderId = _localId, Timestamp = message.Timestamp }, remote);
                }
                else
                {
                    // Unbekannter Client (z.B. nach Host-Neustart) -> kein Pong, Client verbindet neu
                }
                break;

            case MessageType.Event:
                if (_clients.TryGetValue(clientId, out var eventSender))
                {
                    // Lokal ansagen und an alle anderen weiterleiten
                    GroupEventReceived?.Invoke(this, new GroupEvent(message.EventCode, message.Name, clientId));
                    SendEvent(message.EventCode, message.Name, exceptClientId: eventSender.ClientId);
                }
                break;

            case MessageType.Audio:
            case MessageType.Music:
                if (_clients.TryGetValue(clientId, out var sender))
                {
                    sender.LastSeen = DateTime.UtcNow;
                    AudioPacketReceived?.Invoke(this, new AudioPacket
                    {
                        ClientId = clientId,
                        IsMusic = message.Type == MessageType.Music,
                        AudioData = message.Data,
                        Codec = message.Codec,
                        SequenceNumber = message.Sequence,
                        Timestamp = Now
                    });
                }
                break;
        }
    }

    private void HandleClientMessage(NetworkMessage message)
    {
        Interlocked.Exchange(ref _lastHostContact, Now);

        switch (message.Type)
        {
            case MessageType.Accept:
                _hostName = message.Name;
                SetConnected(true);
                _joinTcs?.TrySetResult(true);
                break;

            case MessageType.Pong:
                _latencyMs = (int)(Now - message.Timestamp);
                LatencyUpdated?.Invoke(this, _latencyMs);
                break;

            case MessageType.Event:
                if (_isConnected)
                    GroupEventReceived?.Invoke(this, new GroupEvent(message.EventCode, message.Name, message.SenderId.ToString("N")));
                break;

            case MessageType.Audio:
                if (!_isConnected)
                    return;
                AudioPacketReceived?.Invoke(this, new AudioPacket
                {
                    ClientId = message.SenderId.ToString("N"),
                    AudioData = message.Data,
                    Codec = message.Codec,
                    SequenceNumber = message.Sequence,
                    Timestamp = Now
                });
                break;
        }
    }

    private void Shutdown()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _udp?.Dispose();
        _udp = null;

        _joinTcs?.TrySetResult(false);
        _joinTcs = null;

        _isHost = false;
        _isConnected = false;
        _hostEndPoint = null;
        _hostName = null;
        _latencyMs = 0;
        _clients.Clear();
        ReleaseMulticastLock();
    }

    public static List<string> GetLocalIpAddresses() =>
        System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
            .Select(a => a.ToString())
            .Distinct()
            .ToList();

    // Windows: ICMP "Port unreachable" wuerde sonst ReceiveAsync mit Fehler abbrechen
    private static void IgnoreConnectionReset(UdpClient udp)
    {
        if (OperatingSystem.IsWindows())
        {
            const int SIO_UDP_CONNRESET = -1744830452;
            udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null);
        }
    }

    private void AcquireMulticastLock()
    {
#if ANDROID
        // Noetig, damit Android Broadcast-Pakete (Discover) nicht wegfiltert
        if (_multicastLock == null)
        {
            var wifi = global::Android.App.Application.Context.GetSystemService(global::Android.Content.Context.WifiService) as global::Android.Net.Wifi.WifiManager;
            _multicastLock = wifi?.CreateMulticastLock("WiRide");
            _multicastLock?.SetReferenceCounted(false);
            _multicastLock?.Acquire();
        }
#endif
    }

    private void ReleaseMulticastLock()
    {
#if ANDROID
        _multicastLock?.Release();
        _multicastLock = null;
#endif
    }
}
