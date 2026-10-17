namespace WiRide.Services;

public class HotspotInfo
{
    public string Ssid { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public interface IWiFiService
{
    // Startet einen lokalen Hotspot (Android: LocalOnlyHotspot, SSID/Passwort vergibt das System).
    // null = nicht moeglich -> Fallback: OpenHotspotSettings()
    Task<HotspotInfo?> StartHotspotAsync();
    Task StopHotspotAsync();
    void OpenHotspotSettings();

    Task<List<string>> ScanNetworksAsync();
    Task<bool> ConnectToNetworkAsync(string ssid, string password);
    Task DisconnectFromNetworkAsync();

    bool IsHotspotEnabled { get; }
    HotspotInfo? CurrentHotspot { get; }
    bool IsConnectedToWiFi { get; }
    string? CurrentNetworkSsid { get; }

    // Gateway des aktuellen WLANs = bei Hotspots meist der Host
    string? GatewayIp { get; }
}
