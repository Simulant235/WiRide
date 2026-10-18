using System.Threading.Channels;
using WiRide.Models;

namespace WiRide.Services;

public enum VoiceEngine
{
    System,  // Android-Sprachausgabe
    Piper    // KI-Stimme (muss pro Sprache heruntergeladen werden)
}

// Sprachansagen in der App-Sprache dieses Handys. Ansagen laufen nacheinander in einer Warteschlange.
public class AnnouncementService
{
    private readonly Channel<string> _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(5)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private readonly IPiperSynthesizer? _piper;
    private Locale? _systemLocale;
    private string? _systemLocaleCode;

    public AnnouncementService(IServiceProvider services)
    {
        _piper = services.GetService<IPiperSynthesizer>();
        _ = Task.Run(ProcessQueueAsync);
    }

    public void Announce(string text)
    {
        if (AppSettings.AnnouncementsEnabled && !string.IsNullOrWhiteSpace(text))
            _queue.Writer.TryWrite(text);
    }

    public void AnnounceEvent(GroupEventCode code, string riderName)
    {
        switch (code)
        {
            case GroupEventCode.RiderJoined:
                if (AppSettings.AnnounceRiders)
                    Announce(Loc.T("EvtJoined", riderName));
                break;

            case GroupEventCode.RiderLeft:
                if (AppSettings.AnnounceRiders)
                    Announce(Loc.T("EvtLeft", riderName));
                break;

            default:
                Announce($"{riderName}: {QuickMessageText(code)}");
                break;
        }
    }

    public static string QuickMessageText(GroupEventCode code) => Loc.T($"Qm{code}");

    // Test-Ansage aus den Einstellungen
    public void Test() => Announce(Loc.T("EvtTest"));

    private async Task ProcessQueueAsync()
    {
        await foreach (var text in _queue.Reader.ReadAllAsync())
        {
            try
            {
                await SpeakAsync(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ansage fehlgeschlagen: {ex.Message}");
            }
        }
    }

    private async Task SpeakAsync(string text)
    {
        var language = Loc.CurrentCode;
        float volume = (float)AppSettings.AnnouncementVolume;

        if (AppSettings.VoiceEngine == VoiceEngine.Piper && PiperVoiceManager.IsSupported(language))
        {
            // KI-Stimme gewaehlt: ohne heruntergeladene Stimme bleibt es still
            var dir = PiperVoiceManager.ModelDirectory(language);
            if (_piper != null && dir != null && PiperVoiceManager.IsInstalled(language))
                await _piper.SpeakAsync(dir, text, volume);
            return;
        }

        // Android-Stimme (auch Fallback fuer Sprachen ohne KI-Stimme)
        if (_systemLocaleCode != language)
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            _systemLocale = locales.FirstOrDefault(l => l.Language.StartsWith(language == "no" ? "nb" : language, StringComparison.OrdinalIgnoreCase))
                            ?? locales.FirstOrDefault(l => l.Language.StartsWith(language, StringComparison.OrdinalIgnoreCase));
            _systemLocaleCode = language;
        }

        await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions { Locale = _systemLocale, Volume = volume });
    }
}
