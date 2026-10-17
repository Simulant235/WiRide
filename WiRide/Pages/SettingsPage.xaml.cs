using WiRide.Services;

namespace WiRide.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly IAudioService _audio;

    public SettingsPage()
    {
        InitializeComponent();
        _audio = ServiceHelper.Get<IAudioService>();

        NameEntry.Text = AppSettings.RiderName;
        VolumeSlider.Value = AppSettings.Volume;
        AutoReconnectSwitch.IsToggled = AppSettings.AutoReconnect;
        PortEntry.Text = AppSettings.Port.ToString();

        SampleRatePicker.ItemsSource = AppSettings.SupportedSampleRates.Select(r => $"{r / 1000} kHz").ToList();
        SampleRatePicker.SelectedIndex = Array.IndexOf(AppSettings.SupportedSampleRates, AppSettings.SampleRate);

        VersionLabel.Text = $"WiRide {AppInfo.Current.VersionString}";

        // Lautstaerke sofort hoerbar anpassen
        VolumeSlider.ValueChanged += (_, e) => _audio.Volume = (float)e.NewValue;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!int.TryParse(PortEntry.Text, out var port) || port < 1024 || port > 65535)
        {
            await DisplayAlertAsync("Einstellungen", "Port muss zwischen 1024 und 65535 liegen.", "OK");
            return;
        }

        if (!string.IsNullOrWhiteSpace(NameEntry.Text))
            AppSettings.RiderName = NameEntry.Text;

        AppSettings.Volume = (float)VolumeSlider.Value;
        AppSettings.AutoReconnect = AutoReconnectSwitch.IsToggled;
        AppSettings.Port = port;

        if (SampleRatePicker.SelectedIndex >= 0)
            AppSettings.SampleRate = AppSettings.SupportedSampleRates[SampleRatePicker.SelectedIndex];

        await Navigation.PopAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _audio.Volume = AppSettings.Volume;
        await Navigation.PopAsync();
    }
}
