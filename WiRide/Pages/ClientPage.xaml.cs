using WiRide.Models;
using WiRide.Services;

namespace WiRide.Pages;

public partial class ClientPage : ContentPage
{
    private const string PickWifiOption = "WiRide-WLAN waehlen";
    private const string ManualIpOption = "Host-IP manuell eingeben";

    private readonly IntercomService _intercom;
    private readonly IWiFiService _wifi;
    private bool _busy;

    public ClientPage()
    {
        InitializeComponent();

        _intercom = ServiceHelper.Get<IntercomService>();
        _wifi = ServiceHelper.Get<IWiFiService>();
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        if (!await PermissionHelper.CheckAndRequestAsync(this))
        {
            NetworkStatusLabel.Text = "Berechtigungen verweigert";
            HostNameLabel.Text = "Bitte Berechtigungen in Einstellungen aktivieren";
            ConnectButton.IsEnabled = false;
            return;
        }

        _intercom.StateChanged += OnIntercomStateChanged;
        UpdateUi();
    }

    private async void OnConnectButtonClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        _busy = true;
        ConnectButton.IsEnabled = false;

        try
        {
            var host = await FindHostAsync();
            if (host == null)
                return;

            NetworkStatusLabel.Text = $"Verbinde mit {host.Name}...";
            if (!await _intercom.StartClientAsync(host.IpAddress))
                await DisplayAlertAsync("Verbindung", $"Host {host.IpAddress} antwortet nicht.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Verbindung fehlgeschlagen: {ex.Message}", "OK");
        }
        finally
        {
            _busy = false;
            ConnectButton.IsEnabled = true;
            UpdateUi();
        }
    }

    // 1. Host im aktuellen WLAN suchen  2. WiRide-WLAN waehlen  3. IP manuell
    private async Task<DiscoveredHost?> FindHostAsync()
    {
        var host = await DiscoverAsync();
        if (host != null)
            return host;

        var choice = await DisplayActionSheetAsync("Kein Host im aktuellen WLAN gefunden", "Abbrechen", null,
            PickWifiOption, ManualIpOption);

        if (choice == PickWifiOption)
        {
            var ssid = await NetworkSelectionPage.PickAsync(Navigation);
            if (ssid == null)
                return null;

            var password = await DisplayPromptAsync(ssid, "WLAN-Passwort (wird auf dem Host angezeigt):",
                "Verbinden", "Abbrechen");
            if (password == null)
                return null;

            NetworkStatusLabel.Text = $"Verbinde mit WLAN {ssid}...";
            if (!await _wifi.ConnectToNetworkAsync(ssid, password))
            {
                await DisplayAlertAsync("WLAN", $"Verbindung mit {ssid} fehlgeschlagen.", "OK");
                return null;
            }

            host = await DiscoverAsync();

            // Fallback: beim Hotspot ist der Host fast immer das Gateway
            var gateway = _wifi.GatewayIp;
            if (host == null && gateway != null)
                host = new DiscoveredHost { Name = gateway, IpAddress = gateway };

            return host;
        }

        if (choice == ManualIpOption)
        {
            var ip = await DisplayPromptAsync("Host-IP",
                "Host-IP eingeben (wird auf der Host-Seite angezeigt):",
                "Verbinden", "Abbrechen",
                placeholder: "192.168.43.1",
                initialValue: AppSettings.LastHostIp,
                keyboard: Keyboard.Numeric);

            if (!string.IsNullOrWhiteSpace(ip))
                return new DiscoveredHost { Name = ip.Trim(), IpAddress = ip.Trim() };
        }

        return null;
    }

    private async Task<DiscoveredHost?> DiscoverAsync()
    {
        NetworkStatusLabel.Text = "Suche Host...";

        var extraTargets = new List<string>();
        if (_wifi.GatewayIp is { } gateway)
            extraTargets.Add(gateway);
        if (!string.IsNullOrEmpty(AppSettings.LastHostIp))
            extraTargets.Add(AppSettings.LastHostIp);

        var hosts = await _intercom.Network.DiscoverHostsAsync(AppSettings.Port, extraTargets);

        if (hosts.Count <= 1)
            return hosts.FirstOrDefault();

        var choice = await DisplayActionSheetAsync("Host waehlen", "Abbrechen", null,
            hosts.Select(h => $"{h.Name} ({h.IpAddress})").ToArray());
        return hosts.FirstOrDefault(h => choice == $"{h.Name} ({h.IpAddress})");
    }

    private void OnIntercomStateChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(UpdateUi);

    private void UpdateUi()
    {
        var network = _intercom.Network;
        bool inSession = _intercom.Role == IntercomRole.Client;

        ConnectionSection.IsVisible = !inSession;
        DisconnectButton.IsVisible = inSession;
        MuteIcon.Text = _intercom.IsMuted ? "🔇" : "🎤";

        if (!inSession)
        {
            HostNameLabel.Text = "Nicht verbunden";
            NetworkStatusLabel.Text = "Not Connected";
            LatencyLabel.Text = "Latency: -";
            return;
        }

        HostNameLabel.Text = $"of {network.HostName ?? network.HostIp}";
        if (network.IsConnected)
        {
            NetworkStatusLabel.Text = $"Verbunden mit {network.HostIp}";
            LatencyLabel.Text = $"Latency: {network.LatencyMs} ms";
        }
        else
        {
            NetworkStatusLabel.Text = "Verbindung verloren - verbinde neu...";
            LatencyLabel.Text = "Latency: -";
        }
    }

    private void OnMuteToggled(object? sender, EventArgs e)
    {
        _intercom.IsMuted = !_intercom.IsMuted;
        MuteIcon.Text = _intercom.IsMuted ? "🔇" : "🎤";
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Einstellungen", "Einstellungen kommen in spaeteren Versionen", "OK");
    }

    private async void OnDisconnectClicked(object? sender, EventArgs e)
    {
        await _intercom.StopAsync();
        UpdateUi();
    }

    // Nur beim Zurueck-Navigieren stoppen (nicht beim Oeffnen einer weiteren Seite)
    protected override async void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);

        if (args.NavigationType != NavigationType.Pop)
            return;

        _intercom.StateChanged -= OnIntercomStateChanged;
        await _intercom.StopAsync();
        await _wifi.DisconnectFromNetworkAsync();
    }
}
