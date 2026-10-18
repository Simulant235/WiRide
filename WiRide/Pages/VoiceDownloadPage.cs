using WiRide.Services;

namespace WiRide.Pages;

// Fragt nach dem Download der KI-Stimme fuer die aktuelle Sprache und zeigt den Fortschritt
public class VoiceDownloadPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly CancellationTokenSource _cts = new();

    private VoiceDownloadPage(string language)
    {
        Style = SettingsUi.Style("WrPage");

        var progressBar = new ProgressBar { ProgressColor = ThemeManager.Get("WrAccent") };
        var percent = new Label { Style = SettingsUi.Style("WrCaption"), HorizontalOptions = LayoutOptions.Center, Text = "0 %" };
        var cancel = new Button { Text = Loc.T("Cancel"), Style = SettingsUi.Style("WrSecondaryButton") };
        cancel.Clicked += (_, _) => _cts.Cancel();

        Content = new VerticalStackLayout
        {
            Padding = new Thickness(24),
            Spacing = 18,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                SettingsUi.Icon(Icons.Headset, "WrAccent", 48),
                new Label { Text = Loc.T("VoiceDownloading"), Style = SettingsUi.Style("WrTitle"), HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = Loc.T("VoiceDownloadHint"), Style = SettingsUi.Style("WrCaption"), HorizontalTextAlignment = TextAlignment.Center },
                progressBar,
                percent,
                cancel
            }
        };

        Loaded += async (_, _) =>
        {
            var progress = new Progress<double>(p =>
            {
                progressBar.Progress = p;
                percent.Text = $"{p * 100:0} %";
            });

            bool ok = await PiperVoiceManager.DownloadAsync(language, progress, _cts.Token);
            _result.TrySetResult(ok);
            await Navigation.PopModalAsync();
        };
    }

    protected override bool OnBackButtonPressed()
    {
        _cts.Cancel();
        return true;
    }

    // Fragt nur, wenn KI-Stimme gewaehlt ist und fuer die aktuelle Sprache noch fehlt.
    // force = auch fragen, wenn fuer diese Sprache schon einmal gefragt wurde.
    public static async Task CheckAsync(Page page, bool force = false)
    {
        if (AppSettings.VoiceEngine != VoiceEngine.Piper && !force)
            return;

        var language = Loc.CurrentCode;
        if (!force && AppSettings.PiperPromptedLanguage == language)
            return;
        AppSettings.PiperPromptedLanguage = language;

        var languageName = Loc.Languages.FirstOrDefault(l => l.Code == language)?.NativeName ?? language;

        if (!PiperVoiceManager.IsSupported(language))
        {
            await page.DisplayAlertAsync(Loc.T("VoiceAi"), Loc.T("VoiceNotAvailable", languageName), Loc.T("Ok"));
            return;
        }

        if (PiperVoiceManager.IsInstalled(language))
            return;

        bool download = await page.DisplayAlertAsync(Loc.T("VoiceAi"),
            Loc.T("VoiceDownloadQuestion", languageName, PiperVoiceManager.ApproxDownloadMb),
            Loc.T("Download"), Loc.T("Cancel"));
        if (!download)
            return;

        var downloadPage = new VoiceDownloadPage(language);
        await page.Navigation.PushModalAsync(downloadPage);
        bool ok = await downloadPage._result.Task;

        await page.DisplayAlertAsync(Loc.T("VoiceAi"), Loc.T(ok ? "VoiceDownloadDone" : "VoiceDownloadFailed"), Loc.T("Ok"));
        if (ok)
            ServiceHelper.Get<AnnouncementService>().Test();
    }
}
