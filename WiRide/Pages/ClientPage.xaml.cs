using WiRide.Models;
using WiRide.Services;

namespace WiRide.Pages;

public partial class ClientPage : ContentPage
{
    private readonly IntercomService _intercom;
    private readonly IWiFiService _wifi;
    private bool _busy;
    private bool _permissionsChecked;

    public ClientPage()
    {
        InitializeComponent();

        _intercom = ServiceHelper.Get<IntercomService>();
        _wifi = ServiceHelper.Get<IWiFiService>();
    }

    private bool InSession => _intercom.Role == IntercomRole.Client;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _intercom.StateChanged += OnIntercomStateChanged;
        UpdateUi();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _intercom.StateChanged -= OnIntercomStateChanged;
    }

    private void OnIntercomStateChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(UpdateUi);

    // ---------------- Verbindungswege ----------------

    private async void OnScanQrClicked(object? sender, EventArgs e) => await RunBusyAsync(async () =>
    {
        var credentials = await QrScanPage.ScanAsync(this);
        if (credentials != null)
            await JoinWifiAndConnectAsync(credentials.Ssid, credentials.Password);
    });

    private async void OnSearchClicked(object? sender, EventArgs e) => await RunBusyAsync(async () =>
    {
        var host = await DiscoverAsync();
        if (host == null)
        {
            await DisplayAlertAsync(Loc.T("ClientNoHostTitle"), Loc.T("ClientNoHostText"), Loc.T("Ok"));
            return;
        }

        await ConnectToHostAsync(host);
    });

    private async void OnPickWifiClicked(object? sender, EventArgs e) => await RunBusyAsync(async () =>
    {
        var ssid = await NetworkSelectionPage.PickAsync(Navigation);
        if (ssid == null)
            return;

        var password = await DisplayPromptAsync(ssid, Loc.T("ClientWifiPasswordPrompt"),
            Loc.T("Connect"), Loc.T("Cancel"));
        if (password != null)
            await JoinWifiAndConnectAsync(ssid, password);
    });

    private async void OnManualIpTapped(object? sender, TappedEventArgs e) => await RunBusyAsync(async () =>
    {
        var ip = await DisplayPromptAsync(Loc.T("ClientIpTitle"),
            Loc.T("ClientIpPrompt"),
            Loc.T("Connect"), Loc.T("Cancel"),
            placeholder: "192.168.49.1",
            initialValue: AppSettings.LastHostIp,
            keyboard: Keyboard.Numeric);

        if (!string.IsNullOrWhiteSpace(ip))
            await ConnectToHostAsync(new DiscoveredHost { Name = ip.Trim(), IpAddress = ip.Trim() });
    });

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (_busy || InSession)
            return;

        if (!_permissionsChecked)
        {
            if (!await PermissionHelper.CheckAndRequestAsync(this))
                return;
            _permissionsChecked = true;
        }

        _busy = true;
        BusyIndicator.IsVisible = BusyIndicator.IsRunning = true;
        ConnectSection.IsEnabled = false;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(Loc.T("Error"), Loc.T("ConnectFailed", ex.Message), Loc.T("Ok"));
        }
        finally
        {
            _busy = false;
            BusyIndicator.IsVisible = BusyIndicator.IsRunning = false;
            ConnectSection.IsEnabled = true;
            UpdateUi();
        }
    }

    private async Task JoinWifiAndConnectAsync(string ssid, string password)
    {
        if (_wifi.CurrentNetworkSsid != ssid)
        {
            SetStatus(Loc.T("ClientWifiConnecting"), ssid);
            if (!await _wifi.ConnectToNetworkAsync(ssid, password))
            {
                await DisplayAlertAsync(Loc.T("Wifi"), Loc.T("ClientWifiFailed", ssid), Loc.T("Ok"));
                return;
            }
        }

        // Host suchen, sonst ist beim Hotspot fast immer das Gateway der Host
        var host = await DiscoverAsync();
        if (host == null && _wifi.GatewayIp is { } gateway)
            host = new DiscoveredHost { Name = gateway, IpAddress = gateway };

        if (host == null)
        {
            await DisplayAlertAsync(Loc.T("ClientNoHostTitle"), Loc.T("ClientNoHostInWifi"), Loc.T("Ok"));
            return;
        }

        await ConnectToHostAsync(host);
    }

    private async Task<DiscoveredHost?> DiscoverAsync()
    {
        SetStatus(Loc.T("ClientSearching"), Loc.T("ClientSearchingDetail"));

        var extraTargets = new List<string>();
        if (_wifi.GatewayIp is { } gateway)
            extraTargets.Add(gateway);
        if (!string.IsNullOrEmpty(AppSettings.LastHostIp))
            extraTargets.Add(AppSettings.LastHostIp);

        var hosts = await _intercom.Network.DiscoverHostsAsync(AppSettings.Port, extraTargets);
        if (hosts.Count <= 1)
            return hosts.FirstOrDefault();

        var choice = await DisplayActionSheetAsync(Loc.T("ClientChooseHost"), Loc.T("Cancel"), null,
            hosts.Select(h => $"{h.Name} ({h.IpAddress})").ToArray());
        return hosts.FirstOrDefault(h => choice == $"{h.Name} ({h.IpAddress})");
    }

    private async Task ConnectToHostAsync(DiscoveredHost host)
    {
        SetStatus(Loc.T("ClientConnectingTo", host.Name), host.IpAddress);
        if (!await _intercom.StartClientAsync(host.IpAddress))
            await DisplayAlertAsync(Loc.T("Connection"), Loc.T("ClientHostNoAnswer", host.IpAddress), Loc.T("Ok"));
    }

    // ---------------- Anzeige ----------------

    private void SetStatus(string status, string detail)
    {
        StatusLabel.Text = status;
        DetailLabel.Text = detail;
    }

    private void UpdateUi()
    {
        var network = _intercom.Network;
        bool inSession = InSession;
        var resources = Application.Current!.Resources;

        ConnectSection.IsVisible = !inSession;
        MuteSection.IsVisible = inSession;
        DisconnectButton.IsVisible = inSession;

        if (!inSession)
        {
            StatusDot.Fill = (Color)resources["WrTextMuted"];
            if (!_busy)
                SetStatus(Loc.T("ClientNotConnected"), Loc.T("ClientScanHint"));
            return;
        }

        if (network.IsConnected)
        {
            StatusDot.Fill = (Color)resources["WrSuccess"];
            SetStatus(Loc.T("ClientConnectedTo", network.HostName ?? network.HostIp),
                network.LatencyMs > 0 ? Loc.T("ClientPing", network.LatencyMs) : Loc.T("ClientConnectionOk"));
        }
        else
        {
            StatusDot.Fill = (Color)resources["WrAccent"];
            SetStatus(Loc.T("ClientLost"), Loc.T("ClientReconnecting"));
        }

        UpdateMuteUi();
    }

    private void UpdateMuteUi()
    {
        bool isMuted = _intercom.IsMuted;
        var resources = Application.Current!.Resources;
        MuteIcon.Text = isMuted ? Icons.MicOff : Icons.Mic;
        MuteLabel.Text = Loc.T(isMuted ? "MutedHint" : "UnmutedHint");
        MuteCircle.BackgroundColor = (Color)resources[isMuted ? "WrSurfaceHigh" : "WrAccent"];
        MuteIcon.TextColor = (Color)resources[isMuted ? "WrText" : "WrOnAccent"];
    }

    private void OnMuteToggled(object? sender, EventArgs e)
    {
        _intercom.IsMuted = !_intercom.IsMuted;
        UpdateMuteUi();
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }

    private async void OnDisconnectClicked(object? sender, EventArgs e)
    {
        await _intercom.StopAsync();
        UpdateUi();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await LeaveAsync();

    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () => await LeaveAsync());
        return true;
    }

    private async Task LeaveAsync()
    {
        if (InSession)
        {
            if (!await DisplayAlertAsync(Loc.T("ClientLeaveTitle"), Loc.T("ClientLeaveText"), Loc.T("Leave"), Loc.T("Cancel")))
                return;

            await _intercom.StopAsync();
        }

        await Navigation.PopAsync();
    }
}
