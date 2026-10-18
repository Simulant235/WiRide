using WiRide.Services;

namespace WiRide.Pages;

public partial class HostPage : ContentPage
{
    private readonly IntercomService _intercom;
    private readonly IWiFiService _wifi;
    private IDispatcherTimer? _refreshTimer;
    private HotspotMode _mode;
    private bool _busy;

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
    }

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
            await DisplayAlertAsync("Fehler", ex.Message, "OK");
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

        var setup = await HotspotSetupPage.ShowAsync(Navigation);
        if (setup == null)
            return;

        _mode = setup.Mode;
        StatusLabel.Text = "Starte Gruppe...";

        if (_mode == HotspotMode.Automatic)
        {
            var hotspot = await _wifi.StartHotspotAsync(setup.Password);
            if (hotspot == null)
            {
                var useManual = await DisplayAlertAsync("Automatischer Hotspot nicht möglich",
                    $"{_wifi.LastError}\n\nDu kannst stattdessen den eigenen Handy-Hotspot nutzen.",
                    "Eigener Hotspot", "Abbrechen");
                if (!useManual)
                    return;

                _mode = HotspotMode.Manual;
            }
            else if (hotspot.PasswordFromSystem)
            {
                await DisplayAlertAsync("Hinweis",
                    "Dieses Android erlaubt kein eigenes Passwort. Android hat dafür eines vergeben, es wird dir unten angezeigt.",
                    "OK");
            }
        }

        if (_mode == HotspotMode.Manual)
            _wifi.OpenHotspotSettings();

        await _intercom.StartHostAsync();
    }

    private void UpdateUi()
    {
        bool hosting = IsHosting;
        var success = (Color)Application.Current!.Resources["WrSuccess"];
        var muted = (Color)Application.Current!.Resources["WrTextMuted"];

        StatusDot.Fill = hosting ? success : muted;
        StatusLabel.Text = hosting ? "Gruppe läuft" : "Bereit zum Starten";
        IdleHint.IsVisible = !hosting;
        NetworkInfo.IsVisible = hosting;
        MuteSection.IsVisible = hosting;
        RidersSection.IsVisible = hosting;

        StartStopButton.Text = hosting ? "Gruppe beenden" : "Gruppe starten";
        StartStopButton.Style = (Style)Application.Current.Resources[hosting ? "WrDangerButton" : "WrPrimaryButton"];

        if (!hosting)
            return;

        var hotspot = _wifi.CurrentHotspot;
        QrButton.IsVisible = hotspot != null;
        SsidLabel.Text = hotspot?.Ssid ?? "Eigener Hotspot (Name siehe Android-Einstellungen)";
        PasswordLabel.Text = hotspot?.Password ?? "wie im eigenen Hotspot eingestellt";
        IpLabel.Text = $"IP {string.Join(", ", UdpNetworkService.GetLocalIpAddresses())} · Port {AppSettings.Port}";

        var clients = _intercom.Network.ConnectedClients.OrderBy(c => c.ClientName).ToList();
        RidersTitle.Text = $"Rider ({clients.Count})";
        NoRidersLabel.IsVisible = clients.Count == 0;
        BindableLayout.SetItemsSource(RidersList, clients);

        UpdateMuteUi();
    }

    private void UpdateMuteUi()
    {
        bool isMuted = _intercom.IsMuted;
        MuteIcon.Text = isMuted ? Icons.MicOff : Icons.Mic;
        MuteLabel.Text = isMuted ? "Stumm · tippen zum Sprechen" : "Mikrofon an · tippen zum Stummschalten";
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
            if (!await DisplayAlertAsync("Gruppe beenden?", "Alle Rider werden getrennt.", "Beenden", "Abbrechen"))
                return;

            await _intercom.StopAsync();
        }

        await Navigation.PopAsync();
    }
}
