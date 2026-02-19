namespace WiRide.Services;

public interface IWiFiService
{
    Task<bool> StartHotspotAsync(string ssid, string password);
    Task<bool> StopHotspotAsync();
    Task<bool> ConnectToNetworkAsync(string ssid, string password);
    Task<bool> DisconnectFromNetworkAsync();
    Task<List<string>> ScanNetworksAsync();
    
    bool IsHotspotEnabled { get; }
    bool IsConnectedToWiFi { get; }
    string? CurrentNetworkSsid { get; }
}
