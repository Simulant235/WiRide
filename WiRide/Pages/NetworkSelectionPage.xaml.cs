using WiRide.Services;

namespace WiRide.Pages;

public partial class NetworkSelectionPage : ContentPage
{
    private readonly IWiFiService _wifiService;
    private readonly TaskCompletionSource<string?> _result = new();

    public NetworkSelectionPage()
    {
        InitializeComponent();
        _wifiService = ServiceHelper.Get<IWiFiService>();
        Loaded += OnPageLoaded;
    }

    // Oeffnet die Seite modal und liefert die gewaehlte SSID (oder null)
    public static async Task<string?> PickAsync(INavigation navigation)
    {
        var page = new NetworkSelectionPage();
        await navigation.PushModalAsync(page);
        return await page._result.Task;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        await ScanNetworksAsync();
    }

    private async Task ScanNetworksAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            RefreshButton.IsEnabled = false;
            NoNetworksLabel.IsVisible = false;

            var networks = await _wifiService.ScanNetworksAsync();

            // WiRide-/Hotspot-Netzwerke zuerst (LocalOnlyHotspot heisst meist "AndroidShare_...")
            var sorted = networks
                .OrderByDescending(IsLikelyWiRideNetwork)
                .ThenBy(n => n)
                .ToList();

            NetworksListView.ItemsSource = sorted;
            NoNetworksLabel.IsVisible = sorted.Count == 0;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", $"Netzwerk-Scan fehlgeschlagen: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            RefreshButton.IsEnabled = true;
        }
    }

    private static bool IsLikelyWiRideNetwork(string ssid) =>
        ssid.StartsWith("DIRECT-WR", StringComparison.OrdinalIgnoreCase) ||
        ssid.StartsWith("WiRide", StringComparison.OrdinalIgnoreCase) ||
        ssid.StartsWith("AndroidShare", StringComparison.OrdinalIgnoreCase) ||
        ssid.StartsWith("DIRECT-", StringComparison.OrdinalIgnoreCase);

    private async void OnNetworkSelected(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string networkName)
        {
            _result.TrySetResult(networkName);
            await Navigation.PopModalAsync();
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await ScanNetworksAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _result.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _result.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
