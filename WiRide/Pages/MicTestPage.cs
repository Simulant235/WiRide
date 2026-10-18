using WiRide.Services;

namespace WiRide.Pages;

// Mikrofon testen: Pegelanzeige und Probehoeren (mit Rauschunterdrueckung/Sprachaktivierung wie in der Gruppe)
public class MicTestPage : ContentPage
{
    private readonly IAudioService _audio = ServiceHelper.Get<IAudioService>();
    private readonly AudioProcessor _processor = new();
    private readonly ProgressBar _level;
    private readonly Label _levelText;
    private readonly Label _vadState;
    private readonly Switch _listen;
    private bool _running;

    public MicTestPage()
    {
        Style = SettingsUi.Style("WrPage");

        _level = new ProgressBar { ProgressColor = ThemeManager.Get("WrAccent"), HeightRequest = 12 };
        _levelText = new Label { Style = SettingsUi.Style("WrCaption") };
        _vadState = new Label { Style = SettingsUi.Style("WrBody") };
        _listen = new Switch { OnColor = ThemeManager.Get("WrAccent"), ThumbColor = Colors.White };

        var content = new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                new Label { Text = Loc.T("MicTestHint"), Style = SettingsUi.Style("WrCaption") },
                SettingsUi.Card(new VerticalStackLayout { Spacing = 10, Children = { _level, _levelText, _vadState } }),
                SettingsUi.Card(SettingsUi.Row(Icons.Headset, Loc.T("MicTestListen"), Loc.T("MicTestListenDesc"), _listen))
            }
        };

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            Padding = new Thickness(20, 16)
        };
        root.Add(SettingsUi.Header(this, Loc.T("SetMicTest")), 0, 0);
        root.Add(new ScrollView { Content = content }, 0, 1);
        Content = root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (ServiceHelper.Get<IntercomService>().Role != IntercomRole.None)
        {
            _levelText.Text = Loc.T("MicTestBusy");
            return;
        }

        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
            return;

        _processor.Reset();
        _audio.MaxQueuedChunks = 5;
        _audio.AudioDataCaptured += OnCaptured;
        await _audio.StartPlaybackAsync();
        await _audio.StartRecordingAsync();
        _running = true;
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_running)
            return;

        _running = false;
        _audio.AudioDataCaptured -= OnCaptured;
        await _audio.StopRecordingAsync();
        await _audio.StopPlaybackAsync();
    }

    private void OnCaptured(object? sender, byte[] pcm)
    {
        var samples = AudioProcessor.ToSamples(pcm);
        bool send = _processor.Process(samples, AppSettings.SampleRate);
        double rms = AudioProcessor.Rms(samples);

        if (_listen.IsToggled && send)
            _ = _audio.PlayAudioAsync(AudioProcessor.ToBytes(samples));

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _level.Progress = Math.Clamp(Math.Log10(Math.Max(rms, 1)) / 4.3, 0, 1);
            _levelText.Text = Loc.T("MicTestLevel", (int)rms);
            _vadState.Text = Loc.T(send ? "MicTestSending" : "MicTestSilent");
            _vadState.TextColor = ThemeManager.Get(send ? "WrSuccess" : "WrTextMuted");
        });
    }
}
