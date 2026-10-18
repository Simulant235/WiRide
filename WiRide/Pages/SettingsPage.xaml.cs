using WiRide.Services;

namespace WiRide.Pages;

// Alle Aenderungen werden sofort gespeichert
public partial class SettingsPage : ContentPage
{
    private static readonly string[] CharsetNames = { "Buchstaben & Zahlen", "Nur Zahlen", "Nur Buchstaben" };

    private readonly IAudioService _audio;
    private readonly IWiFiService _wifi;
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();
        _audio = ServiceHelper.Get<IAudioService>();
        _wifi = ServiceHelper.Get<IWiFiService>();

        SampleRatePicker.ItemsSource = AppSettings.SupportedSampleRates.Select(DescribeSampleRate).ToList();
        CharsetPicker.ItemsSource = CharsetNames;
        VersionLabel.Text = $"WiRide {AppInfo.Current.VersionString}";

        LoadValues();
    }

    private static string DescribeSampleRate(int rate) => rate switch
    {
        8000 => "8 kHz · sparsam",
        16000 => "16 kHz · Standard",
        24000 => "24 kHz · besser",
        _ => $"{rate / 1000} kHz · beste Qualität"
    };

    private void LoadValues()
    {
        _loading = true;
        NameEntry.Text = AppSettings.RiderName;
        VolumeSlider.Value = AppSettings.Volume;
        VolumeValueLabel.Text = $"{AppSettings.Volume * 100:0} %";
        OpusSwitch.IsToggled = AppSettings.UseOpus;
        SampleRatePicker.SelectedIndex = Array.IndexOf(AppSettings.SupportedSampleRates, AppSettings.SampleRate);
        CharsetPicker.SelectedIndex = (int)AppSettings.PasswordCharset;
        AutoReconnectSwitch.IsToggled = AppSettings.AutoReconnect;
        PortEntry.Text = AppSettings.Port.ToString();
        UpdatePasswordExample();
        _loading = false;
    }

    private void UpdatePasswordExample() =>
        PasswordExampleLabel.Text = $"Beispiel: {PasswordGenerator.Generate(AppSettings.PasswordCharset)} · eigenes Passwort beim Start möglich";

    private void OnNameUnfocused(object? sender, FocusEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NameEntry.Text))
            AppSettings.RiderName = NameEntry.Text;
        NameEntry.Text = AppSettings.RiderName;
    }

    private void OnVolumeChanged(object? sender, ValueChangedEventArgs e)
    {
        VolumeValueLabel.Text = $"{e.NewValue * 100:0} %";
        if (_loading)
            return;

        AppSettings.Volume = (float)e.NewValue;
        _audio.Volume = (float)e.NewValue;
    }

    private void OnOpusToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loading)
            AppSettings.UseOpus = e.Value;
    }

    private void OnSampleRateChanged(object? sender, EventArgs e)
    {
        if (!_loading && SampleRatePicker.SelectedIndex >= 0)
            AppSettings.SampleRate = AppSettings.SupportedSampleRates[SampleRatePicker.SelectedIndex];
    }

    private void OnCharsetChanged(object? sender, EventArgs e)
    {
        if (_loading || CharsetPicker.SelectedIndex < 0)
            return;

        AppSettings.PasswordCharset = (PasswordCharset)CharsetPicker.SelectedIndex;
        UpdatePasswordExample();
    }

    private void OnAutoReconnectToggled(object? sender, ToggledEventArgs e)
    {
        if (!_loading)
            AppSettings.AutoReconnect = e.Value;
    }

    private async void OnPortUnfocused(object? sender, FocusEventArgs e)
    {
        if (int.TryParse(PortEntry.Text, out var port) && port is >= 1024 and <= 65535)
        {
            AppSettings.Port = port;
            return;
        }

        PortEntry.Text = AppSettings.Port.ToString();
        await DisplayAlertAsync("Port", "Der Port muss zwischen 1024 und 65535 liegen.", "OK");
    }

    private void OnOpenHotspotSettingsClicked(object? sender, EventArgs e) => _wifi.OpenHotspotSettings();

    private void OnOpenAppSettingsClicked(object? sender, EventArgs e) => AppInfo.Current.ShowSettingsUI();

    private void OnOpenLocationSettingsClicked(object? sender, EventArgs e) =>
        OpenAndroidSettings("android.settings.LOCATION_SOURCE_SETTINGS");

    private void OnOpenWifiSettingsClicked(object? sender, EventArgs e) =>
        OpenAndroidSettings("android.settings.WIFI_SETTINGS");

    private static void OpenAndroidSettings(string action)
    {
#if ANDROID
        var intent = new Android.Content.Intent(action);
        intent.AddFlags(Android.Content.ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
#endif
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync("Zurücksetzen?", "Alle Einstellungen außer deinem Namen werden auf Standard gesetzt.", "Zurücksetzen", "Abbrechen"))
            return;

        AppSettings.Reset();
        _audio.Volume = AppSettings.Volume;
        LoadValues();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        OnNameUnfocused(null, new FocusEventArgs(NameEntry, false));
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        // Name auch speichern, wenn per Hardware-Zurueck verlassen wird
        if (!string.IsNullOrWhiteSpace(NameEntry.Text))
            AppSettings.RiderName = NameEntry.Text;
        base.OnDisappearing();
    }
}
