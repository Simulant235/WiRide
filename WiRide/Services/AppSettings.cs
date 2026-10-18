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

    // Opus-Kompression fuer gesendetes Audio (Empfang erkennt den Codec pro Paket)
    public static bool UseOpus
    {
        get => Preferences.Get(nameof(UseOpus), true);
        set => Preferences.Set(nameof(UseOpus), value);
    }

    public const string SystemLanguage = "system";

    public static string Language
    {
        get => Preferences.Get(nameof(Language), SystemLanguage);
        set => Preferences.Set(nameof(Language), value);
    }

    public static ThemeChoice Theme
    {
        get => (ThemeChoice)Preferences.Get(nameof(Theme), (int)ThemeChoice.System);
        set => Preferences.Set(nameof(Theme), (int)value);
    }

    // Index in ThemeManager.FontScales (1 = normal)
    public static int FontScaleIndex
    {
        get => Preferences.Get(nameof(FontScaleIndex), 1);
        set => Preferences.Set(nameof(FontScaleIndex), value);
    }

    // ---- Audio-Verarbeitung (betrifft nur dieses Handy) ----

    // true = niedrige Latenz, false = Kompatibilitaetsmodus (groessere Puffer wie v1.1)
    public static bool LowLatency
    {
        get => Preferences.Get(nameof(LowLatency), true);
        set => Preferences.Set(nameof(LowLatency), value);
    }

    public static bool NoiseReductionEnabled
    {
        get => Preferences.Get(nameof(NoiseReductionEnabled), true);
        set => Preferences.Set(nameof(NoiseReductionEnabled), value);
    }

    public static double NoiseReductionStrength
    {
        get => Preferences.Get(nameof(NoiseReductionStrength), 0.5);
        set => Preferences.Set(nameof(NoiseReductionStrength), Math.Clamp(value, 0, 1));
    }

    // Normalisierung: leise Rider lauter, laute leiser (nur im eigenen Mix)
    public static bool NormalizeEnabled
    {
        get => Preferences.Get(nameof(NormalizeEnabled), true);
        set => Preferences.Set(nameof(NormalizeEnabled), value);
    }

    public static double NormalizeStrength
    {
        get => Preferences.Get(nameof(NormalizeStrength), 0.6);
        set => Preferences.Set(nameof(NormalizeStrength), Math.Clamp(value, 0, 1));
    }

    // Sprachaktivierung: nur senden, wenn gesprochen wird (experimentell)
    public static bool VoiceActivationEnabled
    {
        get => Preferences.Get(nameof(VoiceActivationEnabled), false);
        set => Preferences.Set(nameof(VoiceActivationEnabled), value);
    }

    public static double VoiceActivationSensitivity
    {
        get => Preferences.Get(nameof(VoiceActivationSensitivity), 0.5);
        set => Preferences.Set(nameof(VoiceActivationSensitivity), Math.Clamp(value, 0, 1));
    }

    // ---- Ansagen & Musik ----

    public static bool AnnouncementsEnabled
    {
        get => Preferences.Get(nameof(AnnouncementsEnabled), true);
        set => Preferences.Set(nameof(AnnouncementsEnabled), value);
    }

    public static double AnnouncementVolume
    {
        get => Preferences.Get(nameof(AnnouncementVolume), 0.9);
        set => Preferences.Set(nameof(AnnouncementVolume), Math.Clamp(value, 0, 1));
    }

    public static bool AnnounceRiders
    {
        get => Preferences.Get(nameof(AnnounceRiders), true);
        set => Preferences.Set(nameof(AnnounceRiders), value);
    }

    public static bool AnnounceConnection
    {
        get => Preferences.Get(nameof(AnnounceConnection), true);
        set => Preferences.Set(nameof(AnnounceConnection), value);
    }

    public static VoiceEngine VoiceEngine
    {
        get => (VoiceEngine)Preferences.Get(nameof(VoiceEngine), (int)VoiceEngine.System);
        set => Preferences.Set(nameof(VoiceEngine), (int)value);
    }

    // Fuer welche Sprache zuletzt nach dem KI-Stimmen-Download gefragt wurde
    public static string PiperPromptedLanguage
    {
        get => Preferences.Get(nameof(PiperPromptedLanguage), string.Empty);
        set => Preferences.Set(nameof(PiperPromptedLanguage), value);
    }

    public static bool DuckingEnabled
    {
        get => Preferences.Get(nameof(DuckingEnabled), true);
        set => Preferences.Set(nameof(DuckingEnabled), value);
    }

    public static bool QuickMessagesEnabled
    {
        get => Preferences.Get(nameof(QuickMessagesEnabled), true);
        set => Preferences.Set(nameof(QuickMessagesEnabled), value);
    }

    public static bool MusicSharingEnabled
    {
        get => Preferences.Get(nameof(MusicSharingEnabled), false);
        set => Preferences.Set(nameof(MusicSharingEnabled), value);
    }

    // ---- Letzte Sitzung (Schnellstart) ----

    public const string RoleHost = "host";
    public const string RoleClient = "client";

    public static string LastRole
    {
        get => Preferences.Get(nameof(LastRole), string.Empty);
        set => Preferences.Set(nameof(LastRole), value);
    }

    public static string LastHostName
    {
        get => Preferences.Get(nameof(LastHostName), string.Empty);
        set => Preferences.Set(nameof(LastHostName), value);
    }

    public static string LastWifiSsid
    {
        get => Preferences.Get(nameof(LastWifiSsid), string.Empty);
        set => Preferences.Set(nameof(LastWifiSsid), value);
    }

    public static string LastWifiPassword
    {
        get => Preferences.Get(nameof(LastWifiPassword), string.Empty);
        set => Preferences.Set(nameof(LastWifiPassword), value);
    }

    // Host: zuletzt benutztes Hotspot-Passwort bleibt, bis man es aendert
    public static string LastHotspotPassword
    {
        get => Preferences.Get(nameof(LastHotspotPassword), string.Empty);
        set => Preferences.Set(nameof(LastHotspotPassword), value);
    }

    public static int LastHotspotMode
    {
        get => Preferences.Get(nameof(LastHotspotMode), 0);
        set => Preferences.Set(nameof(LastHotspotMode), value);
    }

    public static bool QuickStartEnabled
    {
        get => Preferences.Get(nameof(QuickStartEnabled), true);
        set => Preferences.Set(nameof(QuickStartEnabled), value);
    }

    // Lautstaerke steigt mit der Geschwindigkeit (GPS)
    public static bool SpeedVolumeEnabled
    {
        get => Preferences.Get(nameof(SpeedVolumeEnabled), false);
        set => Preferences.Set(nameof(SpeedVolumeEnabled), value);
    }

    public static double SpeedVolumeStrength
    {
        get => Preferences.Get(nameof(SpeedVolumeStrength), 0.5);
        set => Preferences.Set(nameof(SpeedVolumeStrength), Math.Clamp(value, 0, 1));
    }

    // ---- Extras ----

    public static bool OnboardingDone
    {
        get => Preferences.Get(nameof(OnboardingDone), false);
        set => Preferences.Set(nameof(OnboardingDone), value);
    }

    // Zufaellige App-ID fuer Feedback (keine echte Geraete-ID)
    public static string InstallId
    {
        get
        {
            var id = Preferences.Get(nameof(InstallId), string.Empty);
            if (id.Length == 0)
            {
                id = Guid.NewGuid().ToString("N")[..12];
                Preferences.Set(nameof(InstallId), id);
            }
            return id;
        }
    }

    // Schwebendes Mini-Fenster mit Rider-Infos, wenn die App im Hintergrund ist
    public static bool OverlayEnabled
    {
        get => Preferences.Get(nameof(OverlayEnabled), false);
        set => Preferences.Set(nameof(OverlayEnabled), value);
    }

    public static bool HeadsetWarning
    {
        get => Preferences.Get(nameof(HeadsetWarning), true);
        set => Preferences.Set(nameof(HeadsetWarning), value);
    }

    public static PasswordCharset PasswordCharset
    {
        get => (PasswordCharset)Preferences.Get(nameof(PasswordCharset), (int)PasswordCharset.LettersAndDigits);
        set => Preferences.Set(nameof(PasswordCharset), (int)value);
    }

    // Alle Einstellungen auf Standard zuruecksetzen (Rider-Name bleibt)
    public static void Reset()
    {
        var name = RiderName;
        var installId = InstallId;
        Preferences.Clear();
        RiderName = name;
        Preferences.Set(nameof(InstallId), installId);
        OnboardingDone = true;
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
