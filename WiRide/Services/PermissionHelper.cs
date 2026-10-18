namespace WiRide.Services;

// Gemeinsame Laufzeit-Berechtigungen fuer Host und Client
public static class PermissionHelper
{
    public static async Task<bool> CheckAndRequestAsync(Page page)
    {
        // Mikrofon
        var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (micStatus != PermissionStatus.Granted)
        {
            micStatus = await Permissions.RequestAsync<Permissions.Microphone>();
            if (micStatus != PermissionStatus.Granted)
            {
                await page.DisplayAlertAsync(Loc.T("Permission"), Loc.T("PermMic"), Loc.T("Ok"));
                return false;
            }
        }

        // Standort (fuer WiFi-Scan und Hotspot)
        var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (locStatus != PermissionStatus.Granted)
        {
            locStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (locStatus != PermissionStatus.Granted)
            {
                await page.DisplayAlertAsync(Loc.T("Permission"), Loc.T("PermLocation"), Loc.T("Ok"));
                return false;
            }
        }

#if ANDROID
        // Nearby WiFi Devices (Android 13+) und Benachrichtigungen fuer Hintergrundbetrieb
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
        {
            var activity = Platform.CurrentActivity;
            if (activity != null)
            {
                var missing = new[]
                    {
                        Android.Manifest.Permission.NearbyWifiDevices,
                        Android.Manifest.Permission.PostNotifications
                    }
                    .Where(p => AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, p) != Android.Content.PM.Permission.Granted)
                    .ToArray();

                if (missing.Length > 0)
                {
                    AndroidX.Core.App.ActivityCompat.RequestPermissions(activity, missing, 1);
                    await Task.Delay(1000);
                }
            }
        }
#endif

        return true;
    }
}
