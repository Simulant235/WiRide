namespace WiRide.Pages;

public partial class StartPage : ContentPage
{
    public StartPage()
    {
        InitializeComponent();

#if DEBUG
        AudioTestButton.IsVisible = true;
#endif
    }

    private async void OnHostButtonClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new HostPage());
    }

    private async void OnClientButtonClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ClientPage());
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingsPage());
    }

    private async void OnAudioTestButtonClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new AudioTestPage());
    }
}
