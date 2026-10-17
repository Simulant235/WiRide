using WiRide.Services;

namespace WiRide.Pages;

public partial class HostPage : ContentPage
{
    private const string HotspotOption = "WiRide-Hotspot starten (empfohlen)";
    private const string ManualHotspotOption = "Handy-Hotspot manuell aktivieren";
    private const string CurrentWifiOption = "Aktuelles WLAN nutzen";

    private readonly IntercomService _intercom;
    private readonly IWiFiService _wifi;
    private IDispatcherTimer? _refreshTimer;

    public HostPage()
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
            NetworkNameLabel.Text = "Bitte Berechtigungen in Einstellungen aktivieren";
            return;
        }

        await SetupNetworkAsync();

        try
        {
            await _intercom.StartHostAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Host konnte nicht gestartet werden: {ex.Message}", "OK");
            await Navigation.PopAsync();
            return;
        }

        _intercom.StateChanged += OnIntercomStateChanged;

        // Client-Latenzen regelmaessig aktualisieren
        _refreshTimer = Dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(1);
        _refreshTimer.Tick += (_, _) => UpdateUi();
        _refreshTimer.Start();

        UpdateUi();
    }

    // Netzwerk fuer die Gruppe bereitstellen: eigener Hotspot, manueller Hotspot oder bestehendes WLAN
    private async Task SetupNetworkAsync()
    {
        var choice = await DisplayActionSheetAsync("Netzwerk fuer die Gruppe", null, null,
            HotspotOption, ManualHotspotOption, CurrentWifiOption);

        if (choice == HotspotOption)
        {
            NetworkStatusLabel.Text = "Starte Hotspot...";
            if (await _wifi.StartHotspotAsync() == null)
            {
                await DisplayAlertAsync("Hotspot",
                    "Der automatische Hotspot konnte nicht gestartet werden (Standort aktiviert?). " +
                    "Bitte den Handy-Hotspot manuell aktivieren.", "Einstellungen oeffnen");
                _wifi.OpenHotspotSettings();
            }
        }
        else if (choice == ManualHotspotOption)
        {
            _wifi.OpenHotspotSettings();
        }
    }

    private void OnIntercomStateChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(UpdateUi);

    private void UpdateUi()
    {
        var clients = _intercom.Network.ConnectedClients.OrderBy(c => c.ClientName).ToList();
        ClientsListView.ItemsSource = clients;
        ClientsListContainer.IsVisible = clients.Count > 0;

        var ips = string.Join(", ", UdpNetworkService.GetLocalIpAddresses());
        NetworkStatusLabel.Text = $"Hosting als {AppSettings.RiderName} - {clients.Count} Rider verbunden";
        var hotspot = _wifi.CurrentHotspot;
        NetworkNameLabel.Text = hotspot != null
            ? $"WLAN: {hotspot.Ssid}  Passwort: {hotspot.Password}\nIP: {ips}"
            : $"IP: {ips} : {AppSettings.Port}";
        MuteIcon.Text = _intercom.IsMuted ? "🔇" : "🎤";
    }

    private void OnMuteToggled(object? sender, EventArgs e)
    {
        _intercom.IsMuted = !_intercom.IsMuted;
        MuteIcon.Text = _intercom.IsMuted ? "🔇" : "🎤";
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }

    private async void OnStopHostClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    // Nur beim Zurueck-Navigieren stoppen (nicht beim Oeffnen einer weiteren Seite)
    protected override async void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);

        if (args.NavigationType != NavigationType.Pop)
            return;

        _refreshTimer?.Stop();
        _intercom.StateChanged -= OnIntercomStateChanged;
        await _intercom.StopAsync();
        await _wifi.StopHotspotAsync();
    }
}
