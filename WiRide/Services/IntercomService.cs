using System.Diagnostics;
using WiRide.Models;

namespace WiRide.Services;

public enum IntercomRole
{
    None,
    Host,
    Client
}

// Verbindet Audio, Netzwerk und Mixer zu einer Intercom-Sitzung.
// Host: empfaengt alle Clients, mischt Mix-Minus und schickt jedem seinen Mix zurueck.
// Client: sendet Mikrofon an Host und spielt den empfangenen Mix ab.
public class IntercomService
{
    public const string HostSourceId = "host";

    // Abspiel-Puffer klein halten (5 x 20 ms = 100 ms) fuer niedrige Latenz
    private const int PlaybackQueueFrames = 5;

    private readonly INetworkService _network;
    private readonly IAudioService _audio;
    private readonly AudioMixerService _mixer;
    private CancellationTokenSource? _mixCts;

    public IntercomService(INetworkService network, IAudioService audio, AudioMixerService mixer)
    {
        _network = network;
        _audio = audio;
        _mixer = mixer;
    }

    public event EventHandler? StateChanged;

    public IntercomRole Role { get; private set; }
    public INetworkService Network => _network;

    public bool IsMuted
    {
        get => _audio.IsMuted;
        set => _audio.IsMuted = value;
    }

    public async Task StartHostAsync()
    {
        await StopAsync();

        _mixer.Clear();
        await _network.StartHostAsync(AppSettings.Port);
        Subscribe();
        Role = IntercomRole.Host;
        await StartAudioAsync();

        _mixCts = new CancellationTokenSource();
        _ = Task.Run(() => MixLoopAsync(_mixCts.Token));

        RaiseStateChanged();
    }

    public async Task<bool> StartClientAsync(string hostIp)
    {
        await StopAsync();

        if (!await _network.ConnectToHostAsync(hostIp, AppSettings.Port))
            return false;

        AppSettings.LastHostIp = hostIp;
        Subscribe();
        Role = IntercomRole.Client;
        await StartAudioAsync();

        RaiseStateChanged();
        return true;
    }

    public async Task StopAsync()
    {
        if (Role == IntercomRole.None)
            return;

        _mixCts?.Cancel();
        _mixCts = null;

        Unsubscribe();
        await _audio.StopRecordingAsync();
        await _audio.StopPlaybackAsync();

        if (Role == IntercomRole.Host)
            await _network.StopHostAsync();
        else
            await _network.DisconnectAsync();

        _mixer.Clear();
        Role = IntercomRole.None;
        RaiseStateChanged();
    }

    private async Task StartAudioAsync()
    {
        _audio.MaxQueuedChunks = PlaybackQueueFrames;
        await _audio.StartPlaybackAsync();
        await _audio.StartRecordingAsync();
    }

    private void Subscribe()
    {
        _audio.AudioDataCaptured += OnAudioCaptured;
        _network.AudioPacketReceived += OnAudioPacketReceived;
        _network.ClientConnected += OnClientConnected;
        _network.ClientDisconnected += OnClientDisconnected;
        _network.ConnectionStateChanged += OnConnectionStateChanged;
        _network.LatencyUpdated += OnLatencyUpdated;
    }

    private void Unsubscribe()
    {
        _audio.AudioDataCaptured -= OnAudioCaptured;
        _network.AudioPacketReceived -= OnAudioPacketReceived;
        _network.ClientConnected -= OnClientConnected;
        _network.ClientDisconnected -= OnClientDisconnected;
        _network.ConnectionStateChanged -= OnConnectionStateChanged;
        _network.LatencyUpdated -= OnLatencyUpdated;
    }

    private void OnAudioCaptured(object? sender, byte[] pcm)
    {
        if (Role == IntercomRole.Host)
            _mixer.AddClientAudio(HostSourceId, pcm);
        else if (Role == IntercomRole.Client)
            _network.SendAudio(pcm, AudioCodecType.Pcm16);
    }

    private void OnAudioPacketReceived(object? sender, AudioPacket packet)
    {
        if (packet.Codec != AudioCodecType.Pcm16)
            return;

        if (Role == IntercomRole.Host)
            _mixer.AddClientAudio(packet.ClientId, packet.AudioData);
        else if (Role == IntercomRole.Client)
            _ = _audio.PlayAudioAsync(packet.AudioData);
    }

    // Host-Mix-Schleife: alle 20 ms einen Frame mischen und verteilen
    private async Task MixLoopAsync(CancellationToken token)
    {
        var clock = Stopwatch.StartNew();
        long tick = 0;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var frame = _mixer.NextFrame(AppSettings.FrameSamples);
                if (frame != null)
                {
                    foreach (var client in _network.ConnectedClients)
                    {
                        var mix = AudioMixerService.MixMinus(frame, client.ClientId);
                        if (mix != null)
                            _network.SendAudio(mix, AudioCodecType.Pcm16, client.ClientId);
                    }

                    var hostMix = AudioMixerService.MixMinus(frame, HostSourceId);
                    if (hostMix != null)
                        await _audio.PlayAudioAsync(hostMix);
                }

                tick++;
                var wait = tick * AppSettings.FrameDurationMs - clock.ElapsedMilliseconds;
                if (wait < -200)
                    tick = clock.ElapsedMilliseconds / AppSettings.FrameDurationMs; // zu weit hinten -> neu synchronisieren
                else if (wait > 0)
                    await Task.Delay((int)wait, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mix-Fehler: {ex.Message}");
            }
        }
    }

    private void OnClientConnected(object? sender, ClientInfo client) => RaiseStateChanged();

    private void OnClientDisconnected(object? sender, string clientId)
    {
        _mixer.RemoveClient(clientId);
        RaiseStateChanged();
    }

    private void OnConnectionStateChanged(object? sender, bool connected) => RaiseStateChanged();

    private void OnLatencyUpdated(object? sender, int latency) => RaiseStateChanged();

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
