using WiRide.Services;
using ZXing.Net.Maui;

namespace WiRide.Pages;

public record WifiCredentials(string Ssid, string Password);

public partial class QrScanPage : ContentPage
{
    private readonly TaskCompletionSource<WifiCredentials?> _result = new();
    private bool _done;

    public QrScanPage()
    {
        InitializeComponent();

        CameraView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
    }

    // Kamera-Berechtigung pruefen, Scanner oeffnen und WLAN-Daten liefern (oder null)
    public static async Task<WifiCredentials?> ScanAsync(Page owner)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            await owner.DisplayAlertAsync("Kamera", "Zum Scannen wird die Kamera-Berechtigung benötigt.", "OK");
            return null;
        }

        var page = new QrScanPage();
        await owner.Navigation.PushModalAsync(page);
        return await page._result.Task;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results.FirstOrDefault()?.Value;
        if (_done || !WifiQrCode.TryParse(value, out var ssid, out var password))
            return;

        _done = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            CameraView.IsDetecting = false;
            try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { }
            _result.TrySetResult(new WifiCredentials(ssid, password));
            await Navigation.PopModalAsync();
        });
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        _result.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _result.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        CameraView.IsDetecting = false;
        base.OnDisappearing();
    }
}
