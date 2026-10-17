using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using WiRide.Services;
using AndroidApp = Android.App.Application;

namespace WiRide.Platforms.Android.Services;

#pragma warning disable CA1422 // Alte WiFi-APIs werden bewusst als Fallback fuer aeltere Android-Versionen genutzt
public class AndroidWiFiService : IWiFiService
{
    private readonly Context _context = AndroidApp.Context;
    private readonly WifiManager? _wifiManager;
    private readonly ConnectivityManager? _connectivityManager;

    private WifiManager.LocalOnlyHotspotReservation? _reservation;
    private HotspotInfo? _hotspot;
    private NetworkCallback? _networkCallback;
    private global::Android.Net.Network? _boundNetwork;

    public AndroidWiFiService()
    {
        _wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
        _connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
    }

    public bool IsHotspotEnabled => _reservation != null;
    public HotspotInfo? CurrentHotspot => _hotspot;
    public bool IsConnectedToWiFi => _boundNetwork != null || (_wifiManager?.ConnectionInfo?.NetworkId ?? -1) != -1;
    public string? CurrentNetworkSsid => _wifiManager?.ConnectionInfo?.SSID?.Replace("\"", "");

    public string? GatewayIp
    {
        get
        {
            int gateway = _wifiManager?.DhcpInfo?.Gateway ?? 0;
            if (gateway == 0)
                return null;

            // DhcpInfo liefert Little-Endian int
            return $"{gateway & 0xFF}.{(gateway >> 8) & 0xFF}.{(gateway >> 16) & 0xFF}.{(gateway >> 24) & 0xFF}";
        }
    }

    // ---------------- Hotspot (Host) ----------------

    public async Task<HotspotInfo?> StartHotspotAsync()
    {
        if (_hotspot != null)
            return _hotspot;

        if (_wifiManager == null || Build.VERSION.SdkInt < BuildVersionCodes.O)
            return null;

        var tcs = new TaskCompletionSource<WifiManager.LocalOnlyHotspotReservation?>();
        try
        {
            _wifiManager.StartLocalOnlyHotspot(new HotspotCallback(tcs), new Handler(Looper.MainLooper!));
        }
        catch (Exception ex)
        {
            // z.B. SecurityException wenn Standort deaktiviert ist
            System.Diagnostics.Debug.WriteLine($"Hotspot-Start fehlgeschlagen: {ex.Message}");
            return null;
        }

        var reservation = await Task.WhenAny(tcs.Task, Task.Delay(10000)) == tcs.Task ? tcs.Task.Result : null;
        if (reservation == null)
            return null;

        _reservation = reservation;
        _hotspot = ReadHotspotInfo(reservation);
        return _hotspot;
    }

    private static HotspotInfo ReadHotspotInfo(WifiManager.LocalOnlyHotspotReservation reservation)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R && reservation.SoftApConfiguration != null)
        {
            var config = reservation.SoftApConfiguration;
            return new HotspotInfo
            {
                Ssid = config.Ssid?.Trim('"') ?? string.Empty,
                Password = config.Passphrase ?? string.Empty
            };
        }

        return new HotspotInfo
        {
            Ssid = reservation.WifiConfiguration?.Ssid?.Trim('"') ?? string.Empty,
            Password = reservation.WifiConfiguration?.PreSharedKey?.Trim('"') ?? string.Empty
        };
    }

    public Task StopHotspotAsync()
    {
        _reservation?.Close();
        _reservation = null;
        _hotspot = null;
        return Task.CompletedTask;
    }

    // Fallback: Nutzer aktiviert den normalen Handy-Hotspot selbst
    public void OpenHotspotSettings()
    {
        var intent = new Intent(Intent.ActionMain);
        intent.SetClassName("com.android.settings", "com.android.settings.TetherSettings");
        intent.AddFlags(ActivityFlags.NewTask);

        try
        {
            _context.StartActivity(intent);
        }
        catch
        {
            var fallback = new Intent(global::Android.Provider.Settings.ActionWirelessSettings);
            fallback.AddFlags(ActivityFlags.NewTask);
            _context.StartActivity(fallback);
        }
    }

    // ---------------- WLAN (Client) ----------------

    public async Task<List<string>> ScanNetworksAsync()
    {
        if (_wifiManager == null)
            return new List<string>();

        try
        {
            // StartScan ist gedrosselt (4x / 2 Min), ScanResults liefert dann den letzten Stand
            if (_wifiManager.StartScan())
                await Task.Delay(3000);

            return (_wifiManager.ScanResults ?? new List<ScanResult>())
                .Select(r => r.Ssid)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WiFi-Scan fehlgeschlagen: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<bool> ConnectToNetworkAsync(string ssid, string password)
    {
        if (_wifiManager == null || _connectivityManager == null)
            return false;

        await DisconnectFromNetworkAsync();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            return await ConnectWithSpecifierAsync(ssid, password);

        return await ConnectLegacyAsync(ssid, password);
    }

    // Android 10+: App-spezifische Verbindung, System zeigt Bestaetigungs-Dialog
    private async Task<bool> ConnectWithSpecifierAsync(string ssid, string password)
    {
        var builder = new WifiNetworkSpecifier.Builder().SetSsid(ssid);
        if (!string.IsNullOrEmpty(password))
            builder.SetWpa2Passphrase(password);

        var request = new NetworkRequest.Builder()
            .AddTransportType(TransportType.Wifi)!
            .RemoveCapability(NetCapability.Internet)!
            .SetNetworkSpecifier(builder.Build())!
            .Build()!;

        var tcs = new TaskCompletionSource<global::Android.Net.Network?>();
        _networkCallback = new NetworkCallback(tcs, () => _boundNetwork = null);
        _connectivityManager!.RequestNetwork(request, _networkCallback);

        var network = await Task.WhenAny(tcs.Task, Task.Delay(30000)) == tcs.Task ? tcs.Task.Result : null;
        if (network == null)
        {
            await DisconnectFromNetworkAsync();
            return false;
        }

        // Gesamten App-Traffic (UDP) ueber dieses WLAN leiten
        _connectivityManager.BindProcessToNetwork(network);
        _boundNetwork = network;
        return true;
    }

    private async Task<bool> ConnectLegacyAsync(string ssid, string password)
    {
        if (!_wifiManager!.IsWifiEnabled)
        {
            _wifiManager.SetWifiEnabled(true);
            await Task.Delay(2000);
        }

        var config = new WifiConfiguration { Ssid = $"\"{ssid}\"" };
        if (string.IsNullOrEmpty(password))
            config.AllowedKeyManagement?.Set((int)KeyManagementType.None);
        else
            config.PreSharedKey = $"\"{password}\"";

        int netId = _wifiManager.AddNetwork(config);
        if (netId == -1)
            return false;

        _wifiManager.Disconnect();
        _wifiManager.EnableNetwork(netId, true);
        _wifiManager.Reconnect();

        // Bis zu 15 s auf Verbindung warten
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(500);
            if (CurrentNetworkSsid == ssid && GatewayIp != null)
                return true;
        }

        return false;
    }

    public Task DisconnectFromNetworkAsync()
    {
        if (_networkCallback != null)
        {
            _connectivityManager?.BindProcessToNetwork(null);
            try { _connectivityManager?.UnregisterNetworkCallback(_networkCallback); } catch { }
            _networkCallback = null;
        }

        _boundNetwork = null;
        return Task.CompletedTask;
    }

    private class HotspotCallback : WifiManager.LocalOnlyHotspotCallback
    {
        private readonly TaskCompletionSource<WifiManager.LocalOnlyHotspotReservation?> _tcs;

        public HotspotCallback(TaskCompletionSource<WifiManager.LocalOnlyHotspotReservation?> tcs) => _tcs = tcs;

        public override void OnStarted(WifiManager.LocalOnlyHotspotReservation? reservation) => _tcs.TrySetResult(reservation);

        public override void OnFailed(LocalOnlyHotspotCallbackErrorCode reason)
        {
            System.Diagnostics.Debug.WriteLine($"LocalOnlyHotspot fehlgeschlagen: {reason}");
            _tcs.TrySetResult(null);
        }

        public override void OnStopped() => _tcs.TrySetResult(null);
    }

    private class NetworkCallback : ConnectivityManager.NetworkCallback
    {
        private readonly TaskCompletionSource<global::Android.Net.Network?> _tcs;
        private readonly Action _onLost;

        public NetworkCallback(TaskCompletionSource<global::Android.Net.Network?> tcs, Action onLost)
        {
            _tcs = tcs;
            _onLost = onLost;
        }

        public override void OnAvailable(global::Android.Net.Network network) => _tcs.TrySetResult(network);

        public override void OnUnavailable() => _tcs.TrySetResult(null);

        public override void OnLost(global::Android.Net.Network network) => _onLost();
    }
}
#pragma warning restore CA1422
