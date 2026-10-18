using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace WiRide
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private static readonly Dictionary<int, TaskCompletionSource<(Result, Intent?)>> PendingResults = new();

        // Startet eine System-Activity (z.B. Aufnahme-Freigabe) und wartet auf das Ergebnis
        public static Task<(Result ResultCode, Intent? Data)> StartActivityForResultAsync(Intent intent, int requestCode)
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
                ?? throw new InvalidOperationException("Keine Activity");

            var tcs = new TaskCompletionSource<(Result, Intent?)>();
            PendingResults[requestCode] = tcs;
            activity.StartActivityForResult(intent, requestCode);
            return tcs.Task;
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (PendingResults.Remove(requestCode, out var tcs))
                tcs.TrySetResult((resultCode, data));
        }
    }
}
