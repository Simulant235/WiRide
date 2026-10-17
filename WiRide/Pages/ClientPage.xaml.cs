using WiRide.Models;
using WiRide.Services;

namespace WiRide.Pages;

public partial class ClientPage : ContentPage
{
    private readonly IntercomService _intercom;
    private bool _busy;

    public ClientPage()
    {
        InitializeComponent();

        _intercom = ServiceHelper.Get<IntercomService>();
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
                await DisplayAlert("Verbindung", $"Host {host.IpAddress} antwortet nicht.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Verbindung fehlgeschlagen: {ex.Message}", "OK");
        }
        finally
        {
            _busy = false;
            ConnectButton.IsEnabled = true;
            UpdateUi();
        }
    }

    // Host per Broadcast suchen, sonst IP manuell eingeben
    private async Task<DiscoveredHost?> FindHostAsync()
    {
        NetworkStatusLabel.Text = "Suche Host...";
        var hosts = await _intercom.Network.DiscoverHostsAsync(AppSettings.Port);

        if (hosts.Count == 1)
            return hosts[0];

        if (hosts.Count > 1)
        {
            var choice = await DisplayActionSheet("Host waehlen", "Abbrechen", null,
                hosts.Select(h => $"{h.Name} ({h.IpAddress})").ToArray());
            return hosts.FirstOrDefault(h => choice == $"{h.Name} ({h.IpAddress})");
        }

        var ip = await DisplayPromptAsync("Kein Host gefunden",
            "Host-IP manuell eingeben (wird auf der Host-Seite angezeigt):",
            "Verbinden", "Abbrechen",
            placeholder: "192.168.43.1",
            initialValue: AppSettings.LastHostIp,
            keyboard: Keyboard.Numeric);

        return string.IsNullOrWhiteSpace(ip) ? null : new DiscoveredHost { Name = ip.Trim(), IpAddress = ip.Trim() };
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
        await DisplayAlert("Einstellungen", "Einstellungen kommen in spaeteren Versionen", "OK");
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
    }
}
