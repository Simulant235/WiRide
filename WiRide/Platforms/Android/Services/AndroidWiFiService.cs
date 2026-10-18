using Android.Content;
using Android.Net;
using Android.Net.Wifi;
using Android.Net.Wifi.P2p;
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
    private WifiP2pManager? _p2pManager;
    private WifiP2pManager.Channel? _p2pChannel;
    private bool _p2pGroupActive;
    private HotspotInfo? _hotspot;
    private NetworkCallback? _networkCallback;
    private global::Android.Net.Network? _boundNetwork;

    public AndroidWiFiService()
    {
        _wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
        _connectivityManager = _context.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
    }

    public string? LastError { get; private set; }
    public bool IsHotspotEnabled => _reservation != null || _p2pGroupActive;
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

    public async Task<HotspotInfo?> StartHotspotAsync(string password)
    {
        if (_hotspot != null)
            await StopHotspotAsync();

        LastError = null;

        if (_wifiManager == null || Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            LastError = "Dieses Geraet unterstuetzt keinen App-Hotspot.";
            return null;
        }

        if (!_wifiManager.IsWifiEnabled)
        {
            LastError = "Bitte zuerst WLAN einschalten (es muss nicht verbunden sein).";
            return null;
        }

        // Android 10+: Wi-Fi Direct Gruppe mit eigenem Namen und Passwort
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            _hotspot = await StartWifiDirectGroupAsync(password);
            if (_hotspot != null)
                return _hotspot;
        }

        // Fallback: LocalOnlyHotspot (Passwort vergibt Android)
        _hotspot = await StartLocalOnlyHotspotAsync();
        if (_hotspot == null)
            LastError ??= "Hotspot konnte nicht gestartet werden. Ist der Standort eingeschaltet?";
        return _hotspot;
    }

    private async Task<HotspotInfo?> StartWifiDirectGroupAsync(string password)
    {
        _p2pManager ??= _context.GetSystemService(Context.WifiP2pService) as WifiP2pManager;
        if (_p2pManager == null)
            return null;

        _p2pChannel ??= _p2pManager.Initialize(_context, Looper.MainLooper, null);
        if (_p2pChannel == null)
            return null;

        // Eventuell noch laufende Gruppe entfernen
        await RunP2pAsync(listener => _p2pManager.RemoveGroup(_p2pChannel, listener));

        // Name muss mit "DIRECT-xy-" beginnen, max. 32 Zeichen
        var name = new string(AppSettings.RiderName.Where(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-').ToArray());
        var ssid = $"DIRECT-WR-WiRide-{name}";
        if (ssid.Length > 32)
            ssid = ssid[..32];

        try
        {
            var config = new WifiP2pConfig.Builder()
                .SetNetworkName(ssid)!
                .SetPassphrase(password)!
                .Build()!;

            if (!await RunP2pAsync(listener => _p2pManager.CreateGroup(_p2pChannel, config, listener)))
                return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Wi-Fi Direct fehlgeschlagen: {ex.Message}");
            return null;
        }

        _p2pGroupActive = true;

        // Gruppeninfo abfragen, um den tatsaechlichen Namen zu bestaetigen
        for (int i = 0; i < 10; i++)
        {
            var group = await RequestGroupInfoAsync();
            if (group?.NetworkName != null)
                return new HotspotInfo { Ssid = group.NetworkName, Password = group.Passphrase ?? password };
            await Task.Delay(300);
        }

        return new HotspotInfo { Ssid = ssid, Password = password };
    }

    private async Task<HotspotInfo?> StartLocalOnlyHotspotAsync()
    {
        var tcs = new TaskCompletionSource<WifiManager.LocalOnlyHotspotReservation?>();
        try
        {
            _wifiManager!.StartLocalOnlyHotspot(new HotspotCallback(tcs), new Handler(Looper.MainLooper!));
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
        var info = ReadHotspotInfo(reservation);
        info.PasswordFromSystem = true;
        return info;
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

    private static async Task<bool> RunP2pAsync(Action<WifiP2pManager.IActionListener> action)
    {
        var listener = new P2pActionListener();
        action(listener);
        var finished = await Task.WhenAny(listener.Task, Task.Delay(5000));
        return finished == listener.Task && listener.Task.Result;
    }

    private async Task<WifiP2pGroup?> RequestGroupInfoAsync()
    {
        var listener = new P2pGroupInfoListener();
        _p2pManager!.RequestGroupInfo(_p2pChannel, listener);
        var finished = await Task.WhenAny(listener.Task, Task.Delay(2000));
        return finished == listener.Task ? listener.Task.Result : null;
    }

    public async Task StopHotspotAsync()
    {
        _reservation?.Close();
        _reservation = null;

        if (_p2pGroupActive && _p2pManager != null && _p2pChannel != null)
            await RunP2pAsync(listener => _p2pManager.RemoveGroup(_p2pChannel, listener));

        _p2pGroupActive = false;
        _hotspot = null;
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

    private class P2pActionListener : Java.Lang.Object, WifiP2pManager.IActionListener
    {
        private readonly TaskCompletionSource<bool> _tcs = new();
        public Task<bool> Task => _tcs.Task;

        public void OnSuccess() => _tcs.TrySetResult(true);

        public void OnFailure(WifiP2pFailureReason reason)
        {
            System.Diagnostics.Debug.WriteLine($"Wi-Fi Direct Fehler: {reason}");
            _tcs.TrySetResult(false);
        }
    }

    private class P2pGroupInfoListener : Java.Lang.Object, WifiP2pManager.IGroupInfoListener
    {
        private readonly TaskCompletionSource<WifiP2pGroup?> _tcs = new();
        public Task<WifiP2pGroup?> Task => _tcs.Task;

        public void OnGroupInfoAvailable(WifiP2pGroup? group) => _tcs.TrySetResult(group);
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
