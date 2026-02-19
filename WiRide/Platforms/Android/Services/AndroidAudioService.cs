using Android.Media;
using WiRide.Services;
using System.Collections.Concurrent;

namespace WiRide.Platforms.Android.Services;

public class AndroidAudioService : IAudioService
{
    private AudioRecord? _audioRecord;
    private AudioTrack? _audioTrack;
    private bool _isRecording = false;
    private bool _isPlaying = false;
    private bool _isMuted = false;
    private float _volume = 0.8f;
    
    private const int SampleRate = 16000;
    private const ChannelIn RecordChannelConfig = ChannelIn.Mono;
    private const ChannelOut PlayChannelConfig = ChannelOut.Mono;
    private const Encoding AudioFormat = Encoding.Pcm16bit;
    private const int BufferSize = 1024;
    
    private readonly ConcurrentQueue<byte[]> _playbackQueue = new();
    private CancellationTokenSource? _recordCts;
    private CancellationTokenSource? _playCts;

    public event EventHandler<byte[]>? AudioDataCaptured;

    public bool IsMuted
    {
        get => _isMuted;
        set => _isMuted = value;
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            _audioTrack?.SetVolume(_volume);
        }
    }

    public bool IsRecording => _isRecording;
    public bool IsPlaying => _isPlaying;

    public Task StartRecordingAsync()
    {
        if (_isRecording)
            return Task.CompletedTask;

        try
        {
            int minBufferSize = AudioRecord.GetMinBufferSize(SampleRate, RecordChannelConfig, AudioFormat);
            _audioRecord = new AudioRecord(
                AudioSource.VoiceCommunication,
                SampleRate,
                RecordChannelConfig,
                AudioFormat,
                Math.Max(minBufferSize, BufferSize * 2));

            if (_audioRecord.State != State.Initialized)
            {
                throw new Exception("AudioRecord initialization failed");
            }

            _audioRecord.StartRecording();
            _isRecording = true;

            _recordCts = new CancellationTokenSource();
            Task.Run(() => RecordingLoop(_recordCts.Token));

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to start recording: {ex.Message}");
        }
    }

    private async Task RecordingLoop(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferSize];

        while (!cancellationToken.IsCancellationRequested && _isRecording)
        {
            try
            {
                int bytesRead = _audioRecord?.Read(buffer, 0, buffer.Length) ?? 0;

                if (bytesRead > 0 && !_isMuted)
                {
                    byte[] audioData = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, audioData, 0, bytesRead);
                    AudioDataCaptured?.Invoke(this, audioData);
                }

                await Task.Delay(10, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Recording error: {ex.Message}");
            }
        }
    }

    public Task StopRecordingAsync()
    {
        if (!_isRecording)
            return Task.CompletedTask;

        _isRecording = false;
        _recordCts?.Cancel();

        _audioRecord?.Stop();
        _audioRecord?.Release();
        _audioRecord?.Dispose();
        _audioRecord = null;

        return Task.CompletedTask;
    }

    public Task StartPlaybackAsync()
    {
        if (_isPlaying)
            return Task.CompletedTask;

        try
        {
            int minBufferSize = AudioTrack.GetMinBufferSize(SampleRate, PlayChannelConfig, AudioFormat);
            
            _audioTrack = new AudioTrack.Builder()
                .SetAudioAttributes(new AudioAttributes.Builder()!
                    .SetUsage(AudioUsageKind.VoiceCommunication)!
                    .SetContentType(AudioContentType.Speech)!
                    .Build()!)
                .SetAudioFormat(new AudioFormat.Builder()!
                    .SetEncoding(AudioFormat)!
                    .SetSampleRate(SampleRate)!
                    .SetChannelMask(PlayChannelConfig)!
                    .Build()!)
                .SetBufferSizeInBytes(Math.Max(minBufferSize, BufferSize * 4))
                .Build();

            if (_audioTrack?.State != AudioTrackState.Initialized)
            {
                throw new Exception("AudioTrack initialization failed");
            }

            _audioTrack.SetVolume(_volume);
            _audioTrack.Play();
            _isPlaying = true;

            _playCts = new CancellationTokenSource();
            Task.Run(() => PlaybackLoop(_playCts.Token));

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to start playback: {ex.Message}");
        }
    }

    private async Task PlaybackLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isPlaying)
        {
            try
            {
                if (_playbackQueue.TryDequeue(out byte[]? audioData) && audioData != null)
                {
                    _audioTrack?.Write(audioData, 0, audioData.Length);
                }
                else
                {
                    await Task.Delay(5, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Playback error: {ex.Message}");
            }
        }
    }

    public Task StopPlaybackAsync()
    {
        if (!_isPlaying)
            return Task.CompletedTask;

        _isPlaying = false;
        _playCts?.Cancel();

        _audioTrack?.Stop();
        _audioTrack?.Release();
        _audioTrack?.Dispose();
        _audioTrack = null;

        while (_playbackQueue.TryDequeue(out _)) { }

        return Task.CompletedTask;
    }

    public Task PlayAudioAsync(byte[] audioData)
    {
        if (_isPlaying && audioData.Length > 0)
        {
            _playbackQueue.Enqueue(audioData);

            // Limit Warteschlange
            while (_playbackQueue.Count > 20)
            {
                _playbackQueue.TryDequeue(out _);
            }
        }

        return Task.CompletedTask;
    }
}
