namespace WiRide.Services;

// Lautstaerke nach Geschwindigkeit: je schneller (mehr Fahrtwind), desto lauter
public class SpeedVolumeService
{
    private const double StartKmh = 20;    // darunter keine Anhebung
    private const double FullKmh = 120;    // ab hier volle Anhebung
    private const double MaxBoost = 1.5;   // +150 % bei voller Staerke

    private readonly IAudioService _audio;
    private bool _running;
    private double _gain = 1;

    public SpeedVolumeService(IAudioService audio) => _audio = audio;

    public double CurrentKmh { get; private set; }

    public async Task StartAsync()
    {
        if (_running || !AppSettings.SpeedVolumeEnabled)
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                Geolocation.Default.LocationChanged += OnLocationChanged;
                _running = await Geolocation.Default.StartListeningForegroundAsync(
                    new GeolocationListeningRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(2)));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GPS fuer Tempo-Lautstaerke nicht verfuegbar: {ex.Message}");
            _running = false;
        }
    }

    public void Stop()
    {
        if (_running)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Geolocation.Default.LocationChanged -= OnLocationChanged;
                Geolocation.Default.StopListeningForeground();
            });
        }

        _running = false;
        _gain = 1;
        CurrentKmh = 0;
        _audio.OutputGain = 1;
    }

    private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        CurrentKmh = Math.Max(0, (e.Location.Speed ?? 0) * 3.6);

        double factor = Math.Clamp((CurrentKmh - StartKmh) / (FullKmh - StartKmh), 0, 1);
        double target = 1 + MaxBoost * AppSettings.SpeedVolumeStrength * factor;

        // Weich nachfuehren, damit es nicht springt
        _gain = _gain * 0.6 + target * 0.4;
        _audio.OutputGain = (float)_gain;
    }
}
