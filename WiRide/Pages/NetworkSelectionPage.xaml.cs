using WiRide.Services;

namespace WiRide.Pages;

public partial class NetworkSelectionPage : ContentPage
{
    private readonly IWiFiService? _wifiService;
    private string? _selectedNetwork;

    public event EventHandler<string>? NetworkSelected;

    public NetworkSelectionPage()
    {
        InitializeComponent();
        _wifiService = Handler?.MauiContext?.Services.GetService<IWiFiService>();
        Loaded += OnPageLoaded;
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

            if (_wifiService != null)
            {
                var networks = await _wifiService.ScanNetworksAsync();
                var wiRideNetworks = networks.Where(n => n.StartsWith("WiRide_")).ToList();

                NetworksListView.ItemsSource = wiRideNetworks;

                if (wiRideNetworks.Count == 0)
                {
                    NoNetworksLabel.IsVisible = true;
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", $"Netzwerk-Scan fehlgeschlagen: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            RefreshButton.IsEnabled = true;
        }
    }

    private async void OnNetworkSelected(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string networkName)
        {
            _selectedNetwork = networkName;

            var connect = await DisplayAlert(
                "Verbinden",
                $"Mit {networkName} verbinden?",
                "Verbinden",
                "Abbrechen");

            if (connect)
            {
                NetworkSelected?.Invoke(this, networkName);
                await Navigation.PopModalAsync();
            }
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await ScanNetworksAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
