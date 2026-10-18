using WiRide.Services;

namespace WiRide.Pages;

public partial class QrCodePage : ContentPage
{
    public QrCodePage(HotspotInfo hotspot)
    {
        InitializeComponent();

        QrView.Value = WifiQrCode.Build(hotspot.Ssid, hotspot.Password);
        SsidLabel.Text = hotspot.Ssid;
        PasswordLabel.Text = hotspot.Password;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
