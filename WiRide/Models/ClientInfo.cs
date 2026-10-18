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

    // Vom Client per Ping gemeldet
    public byte NormalizeStrength { get; set; }
    public int BatteryPercent { get; set; } = -1;
    public int SignalDbm { get; set; }

    // Absender-Adresse inkl. Port (Clients nutzen zufaellige lokale Ports)
    public IPEndPoint? EndPoint { get; set; }

    public string LatencyText => LatencyMs > 0 ? $"{LatencyMs} ms" : WiRide.Services.Loc.T("RiderConnected");
}

public record GroupEvent(GroupEventCode Code, string RiderName, string SenderId);

public class DiscoveredHost
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
