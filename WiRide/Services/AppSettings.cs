namespace WiRide.Services;

// Persistente Einstellungen (MAUI Preferences)
public static class AppSettings
{
    public const int DefaultPort = 8888;
    public const int DefaultSampleRate = 16000;
    public static readonly int[] SupportedSampleRates = { 8000, 16000, 24000, 48000 };

    public static string RiderName
    {
        get
        {
            var name = Preferences.Get(nameof(RiderName), string.Empty);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Rider_{Random.Shared.Next(1000, 9999)}";
                Preferences.Set(nameof(RiderName), name);
            }
            return name;
        }
        set => Preferences.Set(nameof(RiderName), value.Trim());
    }

    public static int Port
    {
        get => Preferences.Get(nameof(Port), DefaultPort);
        set => Preferences.Set(nameof(Port), Math.Clamp(value, 1024, 65535));
    }

    public static int SampleRate
    {
        get
        {
            var rate = Preferences.Get(nameof(SampleRate), DefaultSampleRate);
            return SupportedSampleRates.Contains(rate) ? rate : DefaultSampleRate;
        }
        set => Preferences.Set(nameof(SampleRate), value);
    }

    public static float Volume
    {
        get => Preferences.Get(nameof(Volume), 0.8f);
        set => Preferences.Set(nameof(Volume), Math.Clamp(value, 0f, 1f));
    }

    public static bool AutoReconnect
    {
        get => Preferences.Get(nameof(AutoReconnect), true);
        set => Preferences.Set(nameof(AutoReconnect), value);
    }

    public static string LastHostIp
    {
        get => Preferences.Get(nameof(LastHostIp), string.Empty);
        set => Preferences.Set(nameof(LastHostIp), value);
    }

    // 20 ms pro Audio-Frame (guter Kompromiss Latenz/Overhead, Opus-kompatibel)
    public const int FrameDurationMs = 20;
    public static int FrameSamples => SampleRate * FrameDurationMs / 1000;
    public static int FrameBytes => FrameSamples * 2;
}
