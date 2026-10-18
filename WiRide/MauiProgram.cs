using Microsoft.Extensions.Logging;
using WiRide.Services;
using WiRide.Pages;
using ZXing.Net.Maui.Controls;

#if ANDROID
using WiRide.Platforms.Android.Services;
#endif

namespace WiRide
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "Icons");
                });

            // Seiten registrieren
            builder.Services.AddTransient<StartPage>();
            builder.Services.AddTransient<HostPage>();
            builder.Services.AddTransient<ClientPage>();
            builder.Services.AddTransient<NetworkSelectionPage>();

            // Test-Seiten (nur DEBUG)
#if DEBUG
            builder.Services.AddTransient<AudioTestPage>();
#endif

            // Services registrieren
#if ANDROID
            builder.Services.AddSingleton<IAudioService, AndroidAudioService>();
            builder.Services.AddSingleton<IWiFiService, AndroidWiFiService>();
            builder.Services.AddSingleton<IPiperSynthesizer, AndroidPiperSynthesizer>();
            builder.Services.AddSingleton<IMusicCapture, AndroidMusicCapture>();
#else
            // Platzhalter-Services fuer andere Plattformen
            builder.Services.AddSingleton<IAudioService, MockAudioService>();
            builder.Services.AddSingleton<IWiFiService, MockWiFiService>();
            builder.Services.AddSingleton<IMusicCapture, NoMusicCapture>();
#endif

            builder.Services.AddSingleton<INetworkService, UdpNetworkService>();
            builder.Services.AddSingleton<AudioMixerService>();
            builder.Services.AddSingleton<AudioCodecService>();
            builder.Services.AddSingleton<AnnouncementService>();
            builder.Services.AddSingleton<IntercomService>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }

    // Mock-Services fuer Nicht-Android-Plattformen (zum Testen)
    public class MockAudioService : IAudioService
    {
        public event EventHandler<byte[]>? AudioDataCaptured;
        public bool IsMuted { get; set; }
        public float Volume { get; set; }
        public bool IsRecording => false;
        public bool IsPlaying => false;
        public int MaxQueuedChunks { get; set; }
        public void SetDuckOthers(bool duck) { }
        public Task StartRecordingAsync() => Task.CompletedTask;
        public Task StopRecordingAsync() => Task.CompletedTask;
        public Task StartPlaybackAsync() => Task.CompletedTask;
        public Task StopPlaybackAsync() => Task.CompletedTask;
        public Task PlayAudioAsync(byte[] audioData) => Task.CompletedTask;
    }

    public class MockWiFiService : IWiFiService
    {
        public bool IsHotspotEnabled => false;
        public HotspotInfo? CurrentHotspot => null;
        public bool IsConnectedToWiFi => true;
        public string? CurrentNetworkSsid => null;
        public string? GatewayIp => null;
        public string? LastError => "Nur auf Android verfuegbar";
        public Task<HotspotInfo?> StartHotspotAsync(string password) => Task.FromResult<HotspotInfo?>(null);
        public Task StopHotspotAsync() => Task.CompletedTask;
        public void OpenHotspotSettings() { }
        public Task<bool> ConnectToNetworkAsync(string ssid, string password) => Task.FromResult(false);
        public Task DisconnectFromNetworkAsync() => Task.CompletedTask;
        public Task<List<string>> ScanNetworksAsync() => Task.FromResult(new List<string>());
    }
}
