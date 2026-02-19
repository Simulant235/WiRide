using Android.Net.Wifi;
using Android.Content;
using WiRide.Services;
using AndroidApp = Android.App.Application;

namespace WiRide.Platforms.Android.Services;

public class AndroidWiFiService : IWiFiService
{
    private WifiManager? _wifiManager;
    private bool _isHotspotEnabled = false;

    public AndroidWiFiService()
    {
        var context = AndroidApp.Context;
        _wifiManager = context.GetSystemService(Context.WifiService) as WifiManager;
    }

    public bool IsHotspotEnabled => _isHotspotEnabled;
    public bool IsConnectedToWiFi => _wifiManager?.ConnectionInfo?.NetworkId != -1;
    public string? CurrentNetworkSsid => _wifiManager?.ConnectionInfo?.SSID?.Replace("\"", "");

    public async Task<bool> StartHotspotAsync(string ssid, string password)
    {
        
        await Task.Delay(100); // Kommt in späteren MPVs
        
        System.Diagnostics.Debug.WriteLine($"Hotspot request: {ssid}");
        _isHotspotEnabled = true;
        
        return true;
    }

    public async Task<bool> StopHotspotAsync()
    {
        await Task.Delay(100);
        _isHotspotEnabled = false;
        return true;
    }

    public async Task<bool> ConnectToNetworkAsync(string ssid, string password)
    {
        try
        {
            if (_wifiManager == null)
                return false;

            // Wifi an wenn aus
            if (!_wifiManager.IsWifiEnabled)
            {
                _wifiManager.SetWifiEnabled(true);
                await Task.Delay(2000); // Warte auf wifi
            }
            
            var wifiConfig = new WifiConfiguration
            {
                Ssid = $"\"{ssid}\"",
                PreSharedKey = $"\"{password}\""
            };

            int netId = _wifiManager.AddNetwork(wifiConfig);
            if (netId != -1)
            {
                _wifiManager.Disconnect();
                _wifiManager.EnableNetwork(netId, true);
                _wifiManager.Reconnect();
                
                await Task.Delay(3000); // Warte auf Verbindung
                return IsConnectedToWiFi;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WiFi connection error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DisconnectFromNetworkAsync()
    {
        try
        {
            _wifiManager?.Disconnect();
            await Task.Delay(500);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> ScanNetworksAsync()
    {
        try
        {
            if (_wifiManager == null)
                return new List<string>();

            // An wenn Wifi aus
            if (!_wifiManager.IsWifiEnabled)
            {
                _wifiManager.SetWifiEnabled(true);
                await Task.Delay(2000);
            }

            _wifiManager.StartScan();
            await Task.Delay(3000); // Warte auf Scan-Ergebnisse

            var scanResults = _wifiManager.ScanResults;
            if (scanResults == null)
                return new List<string>();

            return scanResults
                .Where(r => !string.IsNullOrEmpty(r.Ssid))
                .Select(r => r.Ssid!)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WiFi scan error: {ex.Message}");
            return new List<string>();
        }
    }
}
