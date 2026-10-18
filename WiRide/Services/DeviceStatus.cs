namespace WiRide.Services;

// Akku und WLAN-Signal dieses Handys (wird per Ping an den Host gemeldet)
public static class DeviceStatus
{
    public static sbyte BatteryPercent
    {
        get
        {
            try
            {
                var level = Battery.Default.ChargeLevel;
                return level < 0 ? (sbyte)-1 : (sbyte)Math.Round(level * 100);
            }
            catch
            {
                return -1;
            }
        }
    }

    public static short SignalDbm
    {
        get
        {
#if ANDROID
            try
            {
                var wifi = Android.App.Application.Context.GetSystemService(Android.Content.Context.WifiService) as Android.Net.Wifi.WifiManager;
#pragma warning disable CA1422
                var rssi = wifi?.ConnectionInfo?.Rssi ?? 0;
#pragma warning restore CA1422
                return rssi is < 0 and > -127 ? (short)rssi : (short)0;
            }
            catch
            {
                return 0;
            }
#else
            return 0;
#endif
        }
    }

    // Signalstaerke grob in 0-4 Balken
    public static int SignalBars(int dbm) => dbm switch
    {
        0 => -1,
        >= -55 => 4,
        >= -67 => 3,
        >= -75 => 2,
        >= -85 => 1,
        _ => 0
    };
}
