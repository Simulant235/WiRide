namespace WiRide.Services;

// Nimmt den Ton anderer Apps (z.B. Musik-Player) auf, um ihn mit der Gruppe zu teilen.
// Funktioniert nur mit Apps, die das Mitschneiden erlauben (Spotify blockiert es meist).
public interface IMusicCapture
{
    event EventHandler<byte[]>? FrameCaptured;

    bool IsSupported { get; }
    bool IsRunning { get; }

    Task<bool> StartAsync();
    void Stop();
}

public class NoMusicCapture : IMusicCapture
{
    public event EventHandler<byte[]>? FrameCaptured { add { } remove { } }
    public bool IsSupported => false;
    public bool IsRunning => false;
    public Task<bool> StartAsync() => Task.FromResult(false);
    public void Stop() { }
}
