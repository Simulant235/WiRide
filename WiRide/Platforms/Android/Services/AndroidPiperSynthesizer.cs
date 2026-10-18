using Android.Media;
using SherpaOnnx;
using WiRide.Services;

namespace WiRide.Platforms.Android.Services;

// Piper-KI-Stimme ueber sherpa-onnx (offline, auf dem Handy berechnet)
public class AndroidPiperSynthesizer : IPiperSynthesizer
{
    private OfflineTts? _tts;
    private string? _loadedDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task SpeakAsync(string modelDirectory, string text, float volume)
    {
        await _lock.WaitAsync();
        try
        {
            var audio = await Task.Run(() =>
            {
                EnsureLoaded(modelDirectory);
                return _tts!.Generate(text, 1.0f, 0);
            });

            await PlayAsync(audio.Samples, audio.SampleRate, volume);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Piper-Ausgabe fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    private void EnsureLoaded(string modelDirectory)
    {
        if (_tts != null && _loadedDirectory == modelDirectory)
            return;

        _tts?.Dispose();

        var config = new OfflineTtsConfig();
        config.Model.Vits.Model = Directory.GetFiles(modelDirectory, "*.onnx").First();
        config.Model.Vits.Tokens = Path.Combine(modelDirectory, "tokens.txt");
        config.Model.Vits.DataDir = Path.Combine(modelDirectory, "espeak-ng-data");
        config.Model.NumThreads = 2;
        config.Model.Provider = "cpu";
        config.MaxNumSentences = 1;

        _tts = new OfflineTts(config);
        _loadedDirectory = modelDirectory;
    }

    private static async Task PlayAsync(float[] samples, int sampleRate, float volume)
    {
        if (samples.Length == 0)
            return;

        var pcm = new short[samples.Length];
        for (int i = 0; i < samples.Length; i++)
            pcm[i] = (short)Math.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);

        var track = new AudioTrack.Builder()
            .SetAudioAttributes(new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.AssistanceNavigationGuidance)!
                .SetContentType(AudioContentType.Speech)!
                .Build()!)
            .SetAudioFormat(new AudioFormat.Builder()!
                .SetEncoding(Encoding.Pcm16bit)!
                .SetSampleRate(sampleRate)!
                .SetChannelMask(ChannelOut.Mono)!
                .Build()!)
            .SetBufferSizeInBytes(pcm.Length * 2)
            .SetTransferMode(AudioTrackMode.Static)
            .Build();

        try
        {
            track.Write(pcm, 0, pcm.Length);
            track.SetVolume(volume);
            track.Play();
            await Task.Delay((int)(pcm.Length * 1000L / sampleRate) + 150);
            track.Stop();
        }
        finally
        {
            track.Release();
        }
    }
}
