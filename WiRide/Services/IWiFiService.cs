namespace WiRide.Services;

public class HotspotInfo
{
    public string Ssid { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // true = Android hat das Passwort vergeben (Fallback), eigenes Passwort war nicht moeglich
    public bool PasswordFromSystem { get; set; }
}

public interface IWiFiService
{
    // Startet ein Gruppen-WLAN mit eigenem Passwort (Android 10+: Wi-Fi Direct Gruppe,
    // aelter: LocalOnlyHotspot mit System-Passwort). null = nicht moeglich, Grund in LastError.
    Task<HotspotInfo?> StartHotspotAsync(string password);
    Task StopHotspotAsync();
    void OpenHotspotSettings();

    Task<List<string>> ScanNetworksAsync();
    Task<bool> ConnectToNetworkAsync(string ssid, string password);
    Task DisconnectFromNetworkAsync();

    string? LastError { get; }
    bool IsHotspotEnabled { get; }
    HotspotInfo? CurrentHotspot { get; }
    bool IsConnectedToWiFi { get; }
    string? CurrentNetworkSsid { get; }

    // Gateway des aktuellen WLANs = bei Hotspots meist der Host
    string? GatewayIp { get; }
}
