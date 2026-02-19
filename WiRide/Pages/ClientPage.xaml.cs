using WiRide.Services;
using WiRide.Models;

namespace WiRide.Pages;

public partial class ClientPage : ContentPage
{
    // Services - Kommt in spaeteren MVPs
    private readonly IWiFiService? _wifiService;
    private readonly INetworkService? _networkService;
    private readonly IAudioService? _audioService;

    private bool _isMuted = true;
    private bool _isConnected = false;
    private string _selectedNetwork = string.Empty;
    private string _deviceId = Guid.NewGuid().ToString();

    public ClientPage()
    {
        InitializeComponent();

        // Service-Initialisierung - Kommt in spaeteren MVPs
        _wifiService = Handler?.MauiContext?.Services.GetService<IWiFiService>();
        _networkService = Handler?.MauiContext?.Services.GetService<INetworkService>();
        _audioService = Handler?.MauiContext?.Services.GetService<IAudioService>();

        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // MVP 2: Erlaubniss pruefen und anfordern
        if (!await CheckAndRequestPermissionsAsync())
        {
            NetworkStatusLabel.Text = "Erlaubniss verweigert";
            HostNameLabel.Text = "Bitte Berechtigungen in Einstellungen aktivieren";
            return;
        }

        // MVP 1: Nur UI anzeigen, kein Netzwerk-Scanning
        NetworkStatusLabel.Text = "Client Mode (Demo)";
        HostNameLabel.Text = "Verbindungs-Funktionalitaet kommt in spaeteren MVPs";
    }

    private async Task<bool> CheckAndRequestPermissionsAsync()
    {
        // Mikrofon-Erlaubniss
        var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (micStatus != PermissionStatus.Granted)
        {
            micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
            if (micStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Erlaubniss", "Mikrofon-Berechtigung wird benoetigt", "OK");
                return false;
            }
        }

        // Location-Erlaubniss (fuer WiFi)
        var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (locStatus != PermissionStatus.Granted)
        {
            locStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (locStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Erlaubniss", "Standort-Berechtigung wird fuer WiFi benoetigt", "OK");
                return false;
            }
        }

        // Nearby WiFi Erlaubniss fuer Android 13+
#if ANDROID
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity != null)
            {
                var permission = Android.Manifest.Permission.NearbyWifiDevices;
                var status = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, permission);

                if (status != Android.Content.PM.Permission.Granted)
                {
                    AndroidX.Core.App.ActivityCompat.RequestPermissions(activity, new[] { permission }, 1);
                    await Task.Delay(1000);
                }
            }
        }
#endif

        return true;
    }

    private async void OnConnectButtonClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Verbinden", "Netzwerkverbindungs-Funktionalitaet kommt in spaeteren MVPs", "OK");
    }

    private void OnMuteToggled(object? sender, EventArgs e)
    {
        _isMuted = !_isMuted;
        MuteIcon.Text = _isMuted ? "🎤" : "🔇";
    }

    private void OnVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        // Lautstaerke-Funktionalitaet kommt in spaeteren MVPs
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        DisplayAlert("Einstellungen", "Einstellungen kommen in spaeteren MVPs", "OK");
    }

    private async void OnDisconnectClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }
}
