using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net.Wifi;
using Android.OS;
using AndroidX.Core.App;
using WiRide.Services;
using AndroidApp = Android.App.Application;

namespace WiRide.Platforms.Android.Services;

// Haelt Mikrofon, CPU und WLAN aktiv, solange eine Intercom-Sitzung laeuft
// (z.B. bei gesperrtem Bildschirm waehrend der Fahrt).
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMicrophone)]
public class IntercomForegroundService : Service
{
    private const int NotificationId = 1001;
    private const string ChannelId = "wiride_intercom";
    private const string ExtraText = "text";
    private const string ActionStop = "com.wiride.STOP";

    private PowerManager.WakeLock? _wakeLock;
    private WifiManager.WifiLock? _wifiLock;

    public static void Start(string text)
    {
        var context = AndroidApp.Context;
        var intent = new Intent(context, typeof(IntercomForegroundService));
        intent.PutExtra(ExtraText, text);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    public static void Stop()
    {
        var context = AndroidApp.Context;
        context.StopService(new Intent(context, typeof(IntercomForegroundService)));
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // "Beenden" in der Benachrichtigung -> komplette Sitzung stoppen
        if (intent?.Action == ActionStop)
        {
            MainThread.BeginInvokeOnMainThread(async () => await ServiceHelper.Get<IntercomService>().StopAsync());
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        CreateNotificationChannel();

        var stopIntent = new Intent(this, typeof(IntercomForegroundService)).SetAction(ActionStop);
        var stopPending = PendingIntent.GetService(this, 1, stopIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var openApp = PackageManager?.GetLaunchIntentForPackage(PackageName ?? string.Empty);
        var pendingIntent = openApp != null
            ? PendingIntent.GetActivity(this, 0, openApp, PendingIntentFlags.Immutable)
            : null;

        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(Loc.T("NotifTitle"))!
            .SetContentText(intent?.GetStringExtra(ExtraText) ?? string.Empty)!
            .SetSmallIcon(Resource.Mipmap.appicon)!
            .SetOngoing(true)!
            .SetContentIntent(pendingIntent)!
            .AddAction(0, Loc.T("End"), stopPending)!
            .Build()!;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            StartForeground(NotificationId, notification, ForegroundService.TypeMicrophone);
        else
            StartForeground(NotificationId, notification);

        AcquireLocks();
        return StartCommandResult.NotSticky;
    }

    private void AcquireLocks()
    {
        if (_wakeLock == null && GetSystemService(PowerService) is PowerManager power)
        {
            _wakeLock = power.NewWakeLock(WakeLockFlags.Partial, "WiRide::Intercom");
            _wakeLock?.Acquire();
        }

        if (_wifiLock == null && GetSystemService(WifiService) is WifiManager wifi)
        {
#pragma warning disable CA1422 // Ab Android 14 durch LowLatency ersetzt, HighPerf bleibt als Fallback
            var mode = Build.VERSION.SdkInt >= BuildVersionCodes.Q ? global::Android.Net.WifiMode.FullLowLatency : global::Android.Net.WifiMode.FullHighPerf;
#pragma warning restore CA1422
            _wifiLock = wifi.CreateWifiLock(mode, "WiRide::Intercom");
            _wifiLock?.Acquire();
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var channel = new NotificationChannel(ChannelId, Loc.T("NotifChannel"), NotificationImportance.Low)
        {
            Description = Loc.T("NotifChannelDesc")
        };
        (GetSystemService(NotificationService) as NotificationManager)?.CreateNotificationChannel(channel);
    }

    public override void OnDestroy()
    {
        if (_wakeLock?.IsHeld == true)
            _wakeLock.Release();
        if (_wifiLock?.IsHeld == true)
            _wifiLock.Release();

        _wakeLock = null;
        _wifiLock = null;

        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }
}
