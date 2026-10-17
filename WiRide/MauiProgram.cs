using Microsoft.Extensions.Logging;
using WiRide.Services;
using WiRide.Pages;

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
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
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
#else
            // Platzhalter-Services fuer andere Plattformen
            builder.Services.AddSingleton<IAudioService, MockAudioService>();
            builder.Services.AddSingleton<IWiFiService, MockWiFiService>();
#endif

            builder.Services.AddSingleton<INetworkService, UdpNetworkService>();
            builder.Services.AddSingleton<AudioMixerService>();
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
        public Task StartRecordingAsync() => Task.CompletedTask;
        public Task StopRecordingAsync() => Task.CompletedTask;
        public Task StartPlaybackAsync() => Task.CompletedTask;
        public Task StopPlaybackAsync() => Task.CompletedTask;
        public Task PlayAudioAsync(byte[] audioData) => Task.CompletedTask;
    }

    public class MockWiFiService : IWiFiService
    {
        public bool IsHotspotEnabled => false;
        public bool IsConnectedToWiFi => false;
        public string? CurrentNetworkSsid => null;
        public Task<bool> StartHotspotAsync(string ssid, string password) => Task.FromResult(true);
        public Task<bool> StopHotspotAsync() => Task.FromResult(true);
        public Task<bool> ConnectToNetworkAsync(string ssid, string password) => Task.FromResult(true);
        public Task<bool> DisconnectFromNetworkAsync() => Task.FromResult(true);
        public Task<List<string>> ScanNetworksAsync() => Task.FromResult(new List<string> { "WiRide_Test1", "WiRide_Test2" });
    }
}
