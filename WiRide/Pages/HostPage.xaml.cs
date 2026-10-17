using WiRide.Services;
using WiRide.Models;

namespace WiRide.Pages;

public partial class HostPage : ContentPage
{
    // Services - Kommt in spaeteren MVPs
    private readonly IWiFiService? _wifiService;
    private readonly INetworkService? _networkService;
    private readonly IAudioService? _audioService;
    private readonly AudioMixerService _mixerService;

    private bool _isMuted = true;
    private bool _isHosting = false;
    private string _deviceId = Guid.NewGuid().ToString();

    public HostPage()
    {
        InitializeComponent();

        // Service-Initialisierung - Kommt in spaeteren MVPs
        _wifiService = Handler?.MauiContext?.Services.GetService<IWiFiService>();
        _networkService = Handler?.MauiContext?.Services.GetService<INetworkService>();
        _audioService = Handler?.MauiContext?.Services.GetService<IAudioService>();
        _mixerService = new AudioMixerService();

        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        // MVP 2: Permissions pruefen und anfordern
        if (!await CheckAndRequestPermissionsAsync())
        {
            NetworkStatusLabel.Text = "Permissions verweigert";
            NetworkNameLabel.Text = "Bitte Berechtigungen in Einstellungen aktivieren";
            return;
        }

        // MVP 1: Nur UI anzeigen, keine echte Hosting-Funktion
        NetworkStatusLabel.Text = "Host Mode (Demo)";
        NetworkNameLabel.Text = "Netzwerk-Funktionalitaet kommt in spaeteren MVPs";
    }

    private async Task<bool> CheckAndRequestPermissionsAsync()
    {
        // Mikrofon-Permission
        var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (micStatus != PermissionStatus.Granted)
        {
            micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
            if (micStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission", "Mikrofon-Berechtigung wird benoetigt", "OK");
                return false;
            }
        }

        // Location-Permission (fuer WiFi)
        var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (locStatus != PermissionStatus.Granted)
        {
            locStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (locStatus != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission", "Standort-Berechtigung wird fuer WiFi benoetigt", "OK");
                return false;
            }
        }

        // Nearby WiFi Permission fuer Android 13+
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

    private void OnMuteToggled(object? sender, EventArgs e)
    {
        _isMuted = !_isMuted;
        MuteIcon.Text = _isMuted ? "🎤" : "🔇";
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        DisplayAlert("Einstellungen", "Einstellungen kommen in spaeteren MVPs", "OK");
    }

    private async void OnStopHostClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }
}
