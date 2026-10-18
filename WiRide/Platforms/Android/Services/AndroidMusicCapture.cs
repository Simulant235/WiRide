using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Projection;
using WiRide.Services;

namespace WiRide.Platforms.Android.Services;

// Musik teilen (experimentell): AudioPlaybackCapture ab Android 10.
// Braucht die Freigabe "Bildschirm aufnehmen" (es wird nur Ton aufgenommen).
public class AndroidMusicCapture : IMusicCapture
{
    private const int RequestCode = 4711;

    private MediaProjection? _projection;
    private AudioRecord? _record;
    private CancellationTokenSource? _cts;

    public event EventHandler<byte[]>? FrameCaptured;

    public bool IsSupported => OperatingSystem.IsAndroidVersionAtLeast(29);
    public bool IsRunning => _record != null;

    public async Task<bool> StartAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29) || IsRunning)
            return IsRunning;

        var activity = Platform.CurrentActivity;
        if (activity?.GetSystemService(Context.MediaProjectionService) is not MediaProjectionManager manager)
            return false;

        // System-Dialog zur Freigabe anzeigen und Ergebnis abwarten
        var (resultCode, data) = await MainActivity.StartActivityForResultAsync(manager.CreateScreenCaptureIntent(), RequestCode);
        if (resultCode != Result.Ok || data == null)
            return false;

        // Ab Android 14 muss der Foreground-Service vorher den Typ "mediaProjection" haben
        if (!IntercomForegroundService.EnableMediaProjection())
            return false;

        try
        {
            _projection = manager.GetMediaProjection((int)resultCode, data);
            if (_projection == null)
                return false;
            _projection.RegisterCallback(new StopCallback(Stop), null);

            var config = new AudioPlaybackCaptureConfiguration.Builder(_projection)
                .AddMatchingUsage(AudioUsageKind.Media)
                .AddMatchingUsage(AudioUsageKind.Game)
                .AddMatchingUsage(AudioUsageKind.Unknown)
                .Build();

            int frameBytes = AppSettings.FrameBytes;
            _record = new AudioRecord.Builder()
                .SetAudioFormat(new AudioFormat.Builder()
                    .SetEncoding(Encoding.Pcm16bit)!
                    .SetSampleRate(AppSettings.SampleRate)!
                    .SetChannelMask((ChannelOut)(int)ChannelIn.Mono)! // Eingangs-Maske, API kennt nur ChannelOut
                    .Build()!)
                .SetBufferSizeInBytes(frameBytes * 4)
                .SetAudioPlaybackCaptureConfig(config)
                .Build();

            _record.StartRecording();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _ = Task.Run(() => CaptureLoop(frameBytes, token));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Musik-Mitschnitt fehlgeschlagen: {ex.Message}");
            Stop();
            return false;
        }
    }

    private void CaptureLoop(int frameBytes, CancellationToken token)
    {
        var buffer = new byte[frameBytes];
        while (!token.IsCancellationRequested && _record != null)
        {
            int read = _record.Read(buffer, 0, buffer.Length);
            if (read == frameBytes)
                FrameCaptured?.Invoke(this, (byte[])buffer.Clone());
            else if (read <= 0)
                Thread.Sleep(5);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;

        try { _record?.Stop(); } catch { }
        _record?.Release();
        _record = null;

        _projection?.Stop();
        _projection = null;
    }

    private class StopCallback : MediaProjection.Callback
    {
        private readonly Action _onStop;
        public StopCallback(Action onStop) => _onStop = onStop;
        public override void OnStop() => _onStop();
    }
}
