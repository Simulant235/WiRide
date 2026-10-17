using WiRide.Services;

namespace WiRide.Pages;

public partial class AudioTestPage : ContentPage
{
    private IAudioService? _audioService;
    private readonly List<byte[]> _recordedAudioChunks = new();
    private bool _isRecording = false;

    public AudioTestPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var app = Application.Current;
            if (app?.Handler?.MauiContext?.Services != null)
            {
                _audioService = app.Handler.MauiContext.Services.GetService<IAudioService>();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Debug", $"Service-Load Error: {ex.Message}", "OK");
        }

        if (_audioService == null)
        {
            await DisplayAlertAsync("Fehler", 
                "Audio-Service konnte nicht geladen werden.\n\n" +
                "Moegliche Gruende:\n" +
                "- Nur auf Android verfuegbar\n" +
                "- Service nicht registriert", 
                "OK");
            await Navigation.PopAsync();
            return;
        }

        if (!await CheckPermissionsAsync())
        {
            await DisplayAlertAsync("Fehler", "Audio-Berechtigungen fehlen", "OK");
            await Navigation.PopAsync();
        }
    }

    private async Task<bool> CheckPermissionsAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Microphone>();
        }
        return status == PermissionStatus.Granted;
    }

    private async void OnStartRecordingClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_audioService == null)
            {
                await DisplayAlertAsync("Fehler", "Audio-Service nicht verfuegbar", "OK");
                return;
            }

            _recordedAudioChunks.Clear();

            _audioService.AudioDataCaptured += OnAudioDataCaptured;

            await _audioService.StartRecordingAsync();

            _isRecording = true;
            RecordingStatusLabel.Text = "Recording: Yes";
            StartRecordingButton.IsEnabled = false;
            StopRecordingButton.IsEnabled = true;
            PlayRecordingButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Recording-Start fehlgeschlagen: {ex.Message}", "OK");
        }
    }

    private async void OnStopRecordingClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_audioService == null) return;

            await _audioService.StopRecordingAsync();
            _audioService.AudioDataCaptured -= OnAudioDataCaptured;

            _isRecording = false;
            RecordingStatusLabel.Text = "Recording: No";
            AudioLevelLabel.Text = "Audio Level: 0";
            StartRecordingButton.IsEnabled = true;
            StopRecordingButton.IsEnabled = false;

            if (_recordedAudioChunks.Count > 0)
            {
                PlayRecordingButton.IsEnabled = true;
                await DisplayAlertAsync("Info", $"{_recordedAudioChunks.Count} chunks recorded", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Recording-Stop fehlgeschlagen: {ex.Message}", "OK");
        }
    }

    private void OnAudioDataCaptured(object? sender, byte[] audioData)
    {
        _recordedAudioChunks.Add(audioData);

        int level = audioData.Length > 0 ? (int)(audioData.Average(b => b)) : 0;
        int percentage = (int)((level / 255.0) * 100);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AudioLevelLabel.Text = $"Audio Level: {percentage}% ({_recordedAudioChunks.Count} chunks)";
        });
    }

    private async void OnPlayRecordingClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_audioService == null || _recordedAudioChunks.Count == 0)
            {
                await DisplayAlertAsync("Fehler", "Keine Aufnahme vorhanden", "OK");
                return;
            }

            _audioService.Volume = 1.0f;

            await _audioService.StartPlaybackAsync();

            await Task.Delay(100);

            foreach (var chunk in _recordedAudioChunks)
            {
                await _audioService.PlayAudioAsync(chunk);
            }

            int estimatedDurationMs = _recordedAudioChunks.Count * 64;
            await Task.Delay(estimatedDurationMs);

            await DisplayAlertAsync("Info", $"Playback complete ({_recordedAudioChunks.Count} chunks)", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Playback fehlgeschlagen: {ex.Message}", "OK");
        }
    }

    private void OnMuteClicked(object? sender, EventArgs e)
    {
        if (_audioService != null)
        {
            _audioService.IsMuted = true;
            MuteStatusLabel.Text = "Status: MUTED";
        }
    }

    private void OnUnmuteClicked(object? sender, EventArgs e)
    {
        if (_audioService != null)
        {
            _audioService.IsMuted = false;
            MuteStatusLabel.Text = "Status: Not muted";
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (_audioService != null)
        {
            _audioService.AudioDataCaptured -= OnAudioDataCaptured;
            if (_isRecording)
            {
                Task.Run(async () => await _audioService.StopRecordingAsync());
            }
        }
    }
}
