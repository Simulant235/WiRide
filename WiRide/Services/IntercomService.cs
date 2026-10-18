using System.Collections.Concurrent;
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

    // Abspiel-Puffer: niedrige Latenz 3 Frames (60 ms), Kompatibilitaet 5 Frames (100 ms)
    private static int PlaybackQueueFrames => AppSettings.LowLatency ? 3 : 5;
    private static int JitterBufferFrames => AppSettings.LowLatency ? 3 : 4;

    private const double SpeechRms = 600;      // ab hier gilt ein Frame als Sprache (fuer Ducking)
    private const long DuckHoldMs = 700;       // so lange nach dem letzten Wort bleibt die Musik leise

    private readonly AudioProcessor _processor = new();
    private readonly INetworkService _network;
    private readonly IAudioService _audio;
    private readonly AudioMixerService _mixer;
    private readonly AudioCodecService _codec;
    private readonly IWiFiService _wifi;
    private readonly AnnouncementService _announcer;
    private readonly IMusicCapture _music;
    private readonly SpeedVolumeService _speedVolume;
    private readonly ConcurrentDictionary<string, string> _riderNames = new();

    private CancellationTokenSource? _mixCts;
    private Timer? _duckTimer;
    private long _lastSpeechTick;
    private bool _ducking;
    private bool _wasConnected;

    public IntercomService(INetworkService network, IAudioService audio, AudioMixerService mixer,
        AudioCodecService codec, IWiFiService wifi, AnnouncementService announcer, IMusicCapture music,
        SpeedVolumeService speedVolume)
    {
        _speedVolume = speedVolume;
        _network = network;
        _audio = audio;
        _mixer = mixer;
        _codec = codec;
        _wifi = wifi;
        _announcer = announcer;
        _music = music;
        _music.FrameCaptured += OnMusicCaptured;
    }

    public event EventHandler? StateChanged;

    public IntercomRole Role { get; private set; }
    public INetworkService Network => _network;
    public bool IsMusicSharing => _music.IsRunning;
    public bool CanShareMusic => _music.IsSupported && AppSettings.MusicSharingEnabled;

    public bool IsMuted
    {
        get => _audio.IsMuted;
        set => _audio.IsMuted = value;
    }

    public async Task StartHostAsync()
    {
        await StopAsync();

        _mixer.Clear();
        _codec.Clear();
        _riderNames.Clear();
        Role = IntercomRole.Host;

        try
        {
            await _network.StartHostAsync(AppSettings.Port);
            Subscribe();
            await StartAudioAsync();
        }
        catch
        {
            await StopAsync();
            throw;
        }

        _mixCts = new CancellationTokenSource();
        _ = Task.Run(() => MixLoopAsync(_mixCts.Token));

        StartBackgroundMode(Loc.T("NotifHost", AppSettings.RiderName));
        if (AppSettings.AnnounceConnection)
            _announcer.Announce(Loc.T("EvtGroupStarted"));
        RaiseStateChanged();
    }

    public async Task<bool> StartClientAsync(string hostIp)
    {
        await StopAsync();
        _codec.Clear();

        if (!await _network.ConnectToHostAsync(hostIp, AppSettings.Port))
            return false;

        AppSettings.LastHostIp = hostIp;
        AppSettings.LastHostName = _network.HostName ?? hostIp;
        AppSettings.LastRole = AppSettings.RoleClient;
        Subscribe();
        Role = IntercomRole.Client;
        _wasConnected = true;
        await StartAudioAsync();

        var hostName = _network.HostName ?? hostIp;
        StartBackgroundMode(Loc.T("ClientConnectedTo", hostName));
        if (AppSettings.AnnounceConnection)
            _announcer.Announce(Loc.T("EvtConnected", hostName));
        RaiseStateChanged();
        return true;
    }

    public async Task StopAsync()
    {
        if (Role == IntercomRole.None)
        {
            StopBackgroundMode();
            return;
        }

        _mixCts?.Cancel();
        _mixCts = null;
        StopMusicSharing();

        Unsubscribe();
        await _audio.StopRecordingAsync();
        await _audio.StopPlaybackAsync();
        StopDucking();
        _speedVolume.Stop();

        if (Role == IntercomRole.Host)
        {
            await _network.StopHostAsync();
            await _wifi.StopHotspotAsync();
        }
        else
        {
            await _network.DisconnectAsync();
            await _wifi.DisconnectFromNetworkAsync();
        }

        _mixer.Clear();
        _riderNames.Clear();
        StopBackgroundMode();
        Role = IntercomRole.None;
        RaiseStateChanged();
    }

    // ---------------- Schnell-Ansagen ----------------

    public void SendQuickMessage(GroupEventCode code)
    {
        if (Role == IntercomRole.None)
            return;

        _network.SendEvent(code, AppSettings.RiderName);
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
    }

    // ---------------- Musik teilen ----------------

    public async Task<bool> StartMusicSharingAsync()
    {
        if (Role == IntercomRole.None || !CanShareMusic)
            return false;

        bool ok = await _music.StartAsync();
        RaiseStateChanged();
        return ok;
    }

    public void StopMusicSharing()
    {
        if (!_music.IsRunning)
            return;

        _music.Stop();
        _mixer.RemoveClient(AudioMixerService.MusicId(HostSourceId));
        RaiseStateChanged();
    }

    private void OnMusicCaptured(object? sender, byte[] pcm)
    {
        if (Role == IntercomRole.Host)
            _mixer.AddClientAudio(AudioMixerService.MusicId(HostSourceId), pcm);
        else if (Role == IntercomRole.Client)
            _network.SendAudio(_codec.Encode(AudioMixerService.MusicId(HostSourceId), pcm), _codec.OutgoingCodec, isMusic: true);
    }

    // ---------------- Audio ----------------

    private async Task StartAudioAsync()
    {
        _audio.MaxQueuedChunks = PlaybackQueueFrames;
        _audio.Volume = AppSettings.Volume;
        _mixer.MaxBufferedFrames = JitterBufferFrames;
        _processor.Reset();
        await _audio.StartPlaybackAsync();
        await _audio.StartRecordingAsync();

        _duckTimer = new Timer(_ => UpdateDucking(), null, 150, 150);
        await _speedVolume.StartAsync();
    }

    private void Subscribe()
    {
        _audio.AudioDataCaptured += OnAudioCaptured;
        _network.AudioPacketReceived += OnAudioPacketReceived;
        _network.ClientConnected += OnClientConnected;
        _network.ClientDisconnected += OnClientDisconnected;
        _network.ConnectionStateChanged += OnConnectionStateChanged;
        _network.LatencyUpdated += OnLatencyUpdated;
        _network.GroupEventReceived += OnGroupEventReceived;
    }

    private void Unsubscribe()
    {
        _audio.AudioDataCaptured -= OnAudioCaptured;
        _network.AudioPacketReceived -= OnAudioPacketReceived;
        _network.ClientConnected -= OnClientConnected;
        _network.ClientDisconnected -= OnClientDisconnected;
        _network.ConnectionStateChanged -= OnConnectionStateChanged;
        _network.LatencyUpdated -= OnLatencyUpdated;
        _network.GroupEventReceived -= OnGroupEventReceived;
    }

    private void OnAudioCaptured(object? sender, byte[] captured)
    {
        // Eigenes Mikrofon bearbeiten (Rauschunterdrueckung, Sprachaktivierung)
        var samples = AudioProcessor.ToSamples(captured);
        if (!_processor.Process(samples, AppSettings.SampleRate))
            return;

        if (AudioProcessor.Rms(samples) > SpeechRms)
            MarkSpeech();

        var pcm = AudioProcessor.ToBytes(samples);

        if (Role == IntercomRole.Host)
            _mixer.AddClientAudio(HostSourceId, pcm);
        else if (Role == IntercomRole.Client)
            _network.SendAudio(_codec.Encode(HostSourceId, pcm), _codec.OutgoingCodec);
    }

    private void OnAudioPacketReceived(object? sender, AudioPacket packet)
    {
        var streamId = packet.IsMusic ? AudioMixerService.MusicId(packet.ClientId) : packet.ClientId;
        var pcm = _codec.Decode(streamId, packet);
        if (pcm == null || pcm.Length == 0)
            return;

        if (Role == IntercomRole.Host)
        {
            _mixer.AddClientAudio(streamId, pcm);
        }
        else if (Role == IntercomRole.Client)
        {
            if (!packet.IsMusic && HasSpeech(pcm))
                MarkSpeech();
            _ = _audio.PlayAudioAsync(pcm);
        }
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
                        var mix = AudioMixerService.MixMinus(frame, client.ClientId, client.NormalizeStrength / 100.0);
                        if (mix != null)
                            _network.SendAudio(_codec.Encode(client.ClientId, mix), _codec.OutgoingCodec, client.ClientId);
                    }

                    var hostMix = AudioMixerService.MixMinus(frame, HostSourceId,
                        AppSettings.NormalizeEnabled ? AppSettings.NormalizeStrength : 0);
                    if (hostMix != null)
                    {
                        if (frame.Parts.Any(p => p.Key != HostSourceId && !p.Key.EndsWith(AudioMixerService.MusicSuffix)
                                                 && AudioProcessor.Rms(p.Value) > SpeechRms))
                            MarkSpeech();
                        await _audio.PlayAudioAsync(hostMix);
                    }
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

    // ---------------- Ducking (Musik/Navi leiser beim Sprechen) ----------------

    private static bool HasSpeech(byte[] pcm) => AudioProcessor.Rms(AudioProcessor.ToSamples(pcm)) > SpeechRms;

    private void MarkSpeech() => Interlocked.Exchange(ref _lastSpeechTick, Environment.TickCount64);

    private void UpdateDucking()
    {
        bool duck = AppSettings.DuckingEnabled && Role != IntercomRole.None
                    && Environment.TickCount64 - Interlocked.Read(ref _lastSpeechTick) < DuckHoldMs;

        if (duck == _ducking)
            return;

        _ducking = duck;
        _audio.SetDuckOthers(duck);
    }

    private void StopDucking()
    {
        _duckTimer?.Dispose();
        _duckTimer = null;
        _ducking = false;
        _audio.SetDuckOthers(false);
    }

    // ---------------- Ereignisse ----------------

    private void OnClientConnected(object? sender, ClientInfo client)
    {
        _riderNames[client.ClientId] = client.ClientName;
        _announcer.AnnounceEvent(GroupEventCode.RiderJoined, client.ClientName);
        _network.SendEvent(GroupEventCode.RiderJoined, client.ClientName, exceptClientId: client.ClientId);
        RaiseStateChanged();
    }

    private void OnClientDisconnected(object? sender, string clientId)
    {
        _mixer.RemoveClient(clientId);
        _codec.Remove(clientId);
        _codec.Remove(AudioMixerService.MusicId(clientId));

        if (_riderNames.TryRemove(clientId, out var name))
        {
            _announcer.AnnounceEvent(GroupEventCode.RiderLeft, name);
            _network.SendEvent(GroupEventCode.RiderLeft, name);
        }
        RaiseStateChanged();
    }

    private void OnGroupEventReceived(object? sender, GroupEvent e)
    {
        if (e.Code >= GroupEventCode.Pause && !AppSettings.QuickMessagesEnabled)
            return;

        _announcer.AnnounceEvent(e.Code, e.RiderName);
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        if (AppSettings.AnnounceConnection && connected != _wasConnected)
            _announcer.Announce(Loc.T(connected ? "EvtReconnected" : "EvtConnectionLost"));

        _wasConnected = connected;
        RaiseStateChanged();
    }

    private void OnLatencyUpdated(object? sender, int latency) => RaiseStateChanged();

    // Android: Foreground-Service, damit Audio bei gesperrtem Bildschirm weiterlaeuft
    private static void StartBackgroundMode(string text)
    {
#if ANDROID
        Platforms.Android.Services.IntercomForegroundService.Start(text);
#endif
    }

    private static void StopBackgroundMode()
    {
#if ANDROID
        Platforms.Android.Services.IntercomForegroundService.Stop();
#endif
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
