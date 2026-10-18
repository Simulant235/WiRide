using System.Net;

namespace WiRide.Models;

public class ClientInfo
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
    public bool IsConnected { get; set; }
    public int LatencyMs { get; set; }

    // Absender-Adresse inkl. Port (Clients nutzen zufaellige lokale Ports)
    public IPEndPoint? EndPoint { get; set; }

    public string LatencyText => LatencyMs > 0 ? $"{LatencyMs} ms" : "verbunden";
}

public class DiscoveredHost
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
