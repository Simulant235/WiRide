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

    // z.B. "🔋 80 % · 📶 3/4 · 25 ms"
    public string LatencyText
    {
        get
        {
            var parts = new List<string>();
            if (BatteryPercent >= 0)
                parts.Add($"🔋 {BatteryPercent} %");
            int bars = WiRide.Services.DeviceStatus.SignalBars(SignalDbm);
            if (bars >= 0)
                parts.Add($"📶 {bars}/4");
            if (LatencyMs > 0)
                parts.Add($"{LatencyMs} ms");
            return parts.Count > 0 ? string.Join(" · ", parts) : WiRide.Services.Loc.T("RiderConnected");
        }
    }

    public bool IsWeak => (BatteryPercent is >= 0 and < 20) || WiRide.Services.DeviceStatus.SignalBars(SignalDbm) is 0 or 1;
}

public record GroupEvent(GroupEventCode Code, string RiderName, string SenderId);

public class DiscoveredHost
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
