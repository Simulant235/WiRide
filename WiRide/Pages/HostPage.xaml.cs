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
        // MVP 1: Nur UI anzeigen, keine echte Hosting-Funktion
        NetworkStatusLabel.Text = "Host Mode (Demo)";
        NetworkNameLabel.Text = "Netzwerk-Funktionalitaet kommt in spaeteren MVPs";
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

    private async void OnStopHostClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }
}
