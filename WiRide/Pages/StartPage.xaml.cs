namespace WiRide.Pages;

public partial class StartPage : ContentPage
{
    public StartPage()
    {
        InitializeComponent();
    }

    private async void OnHostButtonClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new HostPage());
    }

    private async void OnClientButtonClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new ClientPage());
    }
}
