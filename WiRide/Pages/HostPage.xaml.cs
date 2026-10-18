using WiRide.Services;

namespace WiRide.Pages;

public partial class HostPage : ContentPage
{
    private readonly IntercomService _intercom;
    private readonly IWiFiService _wifi;
    private IDispatcherTimer? _refreshTimer;
    private HotspotMode _mode;
    private bool _busy;
    private bool _autoStart;

    // autoStart = Schnellstart: mit letztem Modus und Passwort sofort hosten
    public HostPage(bool autoStart) : this()
    {
        _autoStart = autoStart;
    }

    public HostPage()
    {
        InitializeComponent();

        _intercom = ServiceHelper.Get<IntercomService>();
        _wifi = ServiceHelper.Get<IWiFiService>();
    }

    private bool IsHosting => _intercom.Role == IntercomRole.Host;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _intercom.StateChanged += OnIntercomStateChanged;

        // Rider-Latenzen regelmaessig aktualisieren
        _refreshTimer ??= Dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(1);
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();

        UpdateUi();

        if (_autoStart)
        {
            _autoStart = false;
            Dispatcher.Dispatch(() => OnStartStopClicked(null, EventArgs.Empty));
        }
    }

    // Schnellstart nutzt die Werte der letzten Sitzung
    private bool _useLastSetup;

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _intercom.StateChanged -= OnIntercomStateChanged;
        if (_refreshTimer != null)
        {
            _refreshTimer.Tick -= OnRefreshTick;
            _refreshTimer.Stop();
        }
    }

    private void OnRefreshTick(object? sender, EventArgs e)
    {
        if (IsHosting)
            UpdateUi();
    }

    private void OnIntercomStateChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(UpdateUi);

    private async void OnStartStopClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        _useLastSetup = sender == null;

        _busy = true;
        StartStopButton.IsEnabled = false;

        try
        {
            if (IsHosting)
                await _intercom.StopAsync();
            else
                await StartHostingAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(Loc.T("Error"), ex.Message, Loc.T("Ok"));
        }
        finally
        {
            _busy = false;
            StartStopButton.IsEnabled = true;
            UpdateUi();
        }
    }

    private async Task StartHostingAsync()
    {
        if (!await PermissionHelper.CheckAndRequestAsync(this))
            return;

        if (!await PermissionHelper.ConfirmHeadsetAsync(this))
            return;

        var setup = _useLastSetup && PasswordGenerator.Validate(AppSettings.LastHotspotPassword) == null
            ? new HotspotSetupResult((HotspotMode)AppSettings.LastHotspotMode, AppSettings.LastHotspotPassword)
            : await HotspotSetupPage.ShowAsync(Navigation);
        if (setup == null)
            return;

        _mode = setup.Mode;
        StatusLabel.Text = Loc.T("HostStarting");

        if (_mode == HotspotMode.Automatic)
        {
            var hotspot = await _wifi.StartHotspotAsync(setup.Password);
            if (hotspot == null)
            {
                var useManual = await DisplayAlertAsync(Loc.T("HostAutoFailedTitle"),
                    Loc.T("HostAutoFailedText", _wifi.LastError),
                    Loc.T("OwnHotspot"), Loc.T("Cancel"));
                if (!useManual)
                    return;

                _mode = HotspotMode.Manual;
            }
            else if (hotspot.PasswordFromSystem)
            {
                await DisplayAlertAsync(Loc.T("Notice"), Loc.T("HostSystemPassword"), Loc.T("Ok"));
            }
        }

        if (_mode == HotspotMode.Manual)
            _wifi.OpenHotspotSettings();

        await _intercom.StartHostAsync();
        AppSettings.LastRole = AppSettings.RoleHost;
    }

    private void UpdateUi()
    {
        bool hosting = IsHosting;
        var success = (Color)Application.Current!.Resources["WrSuccess"];
        var muted = (Color)Application.Current!.Resources["WrTextMuted"];

        StatusDot.Fill = hosting ? success : muted;
        StatusLabel.Text = Loc.T(hosting ? "HostRunning" : "HostReady");
        IdleHint.IsVisible = !hosting;
        NetworkInfo.IsVisible = hosting;
        MuteSection.IsVisible = hosting;
        UpdateQuickMessages(hosting);
        RidersSection.IsVisible = hosting;

        StartStopButton.Text = Loc.T(hosting ? "GroupStop" : "GroupStart");
        StartStopButton.Style = (Style)Application.Current.Resources[hosting ? "WrDangerButton" : "WrPrimaryButton"];

        if (!hosting)
            return;

        var hotspot = _wifi.CurrentHotspot;
        QrButton.IsVisible = hotspot != null;
        SsidLabel.Text = hotspot?.Ssid ?? Loc.T("HostOwnSsid");
        PasswordLabel.Text = hotspot?.Password ?? Loc.T("HostOwnPassword");
        IpLabel.Text = Loc.T("HostIpInfo", string.Join(", ", UdpNetworkService.GetLocalIpAddresses()), AppSettings.Port);

        var clients = _intercom.Network.ConnectedClients.OrderBy(c => c.ClientName).ToList();
        RidersTitle.Text = Loc.T("RidersCount", clients.Count);
        NoRidersLabel.IsVisible = clients.Count == 0;
        BindableLayout.SetItemsSource(RidersList, clients);

        UpdateMuteUi();
    }

    // Schnell-Ansagen und Musik nur waehrend einer Sitzung anzeigen
    private void UpdateQuickMessages(bool inSession)
    {
        QuickHost.IsVisible = inSession;
        if (!inSession)
            QuickHost.Content = null;
        else if (QuickHost.Content is QuickMessagesView view)
            view.Refresh();
        else
            QuickHost.Content = new QuickMessagesView();
    }

    private void UpdateMuteUi()
    {
        bool isMuted = _intercom.IsMuted;
        MuteIcon.Text = isMuted ? Icons.MicOff : Icons.Mic;
        MuteLabel.Text = Loc.T(isMuted ? "MutedHint" : "UnmutedHint");
        MuteCircle.BackgroundColor = (Color)Application.Current!.Resources[isMuted ? "WrSurfaceHigh" : "WrAccent"];
        MuteIcon.TextColor = (Color)Application.Current.Resources[isMuted ? "WrText" : "WrOnAccent"];
    }

    private void OnMuteToggled(object? sender, EventArgs e)
    {
        _intercom.IsMuted = !_intercom.IsMuted;
        UpdateMuteUi();
    }

    private async void OnQrClicked(object? sender, EventArgs e)
    {
        if (_wifi.CurrentHotspot is { } hotspot)
            await Navigation.PushModalAsync(new QrCodePage(hotspot));
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await LeaveAsync();

    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () => await LeaveAsync());
        return true;
    }

    // Beim Verlassen laufende Gruppe nach Rueckfrage beenden
    private async Task LeaveAsync()
    {
        if (IsHosting)
        {
            if (!await DisplayAlertAsync(Loc.T("HostLeaveTitle"), Loc.T("HostLeaveText"), Loc.T("End"), Loc.T("Cancel")))
                return;

            await _intercom.StopAsync();
        }

        await Navigation.PopAsync();
    }
}
