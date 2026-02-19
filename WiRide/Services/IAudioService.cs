namespace WiRide.Services;

public interface IAudioService
{
    event EventHandler<byte[]>? AudioDataCaptured;
    
    Task StartRecordingAsync();
    Task StopRecordingAsync();
    Task StartPlaybackAsync();
    Task StopPlaybackAsync();
    Task PlayAudioAsync(byte[] audioData);
    
    bool IsMuted { get; set; }
    float Volume { get; set; }
    bool IsRecording { get; }
    bool IsPlaying { get; }
}
