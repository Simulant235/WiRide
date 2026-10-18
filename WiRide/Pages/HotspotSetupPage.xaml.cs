using WiRide.Services;

namespace WiRide.Pages;

public enum HotspotMode
{
    Automatic,
    Manual
}

public record HotspotSetupResult(HotspotMode Mode, string Password);

public partial class HotspotSetupPage : ContentPage
{
    private readonly TaskCompletionSource<HotspotSetupResult?> _result = new();
    private HotspotMode _mode = HotspotMode.Automatic;

    public HotspotSetupPage()
    {
        InitializeComponent();

        // Letztes Passwort bleibt, bis man es aendert; sonst neu generieren
        var last = AppSettings.LastHotspotPassword;
        if (PasswordGenerator.Validate(last) == null)
        {
            PasswordEntry.Placeholder = last;
            PasswordEntry.Text = last;
        }
        else
        {
            RegeneratePassword();
        }

        _mode = (HotspotMode)AppSettings.LastHotspotMode;
        UpdateSelection();
    }

    // Oeffnet den Dialog modal und liefert die Auswahl (oder null bei Abbruch)
    public static async Task<HotspotSetupResult?> ShowAsync(INavigation navigation)
    {
        var page = new HotspotSetupPage();
        await navigation.PushModalAsync(page);
        return await page._result.Task;
    }

    private void RegeneratePassword()
    {
        var generated = PasswordGenerator.Generate(AppSettings.PasswordCharset);
        PasswordEntry.Placeholder = generated;
        PasswordEntry.Text = generated;
    }

    private void UpdateSelection()
    {
        var accent = (Color)Application.Current!.Resources["WrAccent"];
        AutoCard.Stroke = _mode == HotspotMode.Automatic ? accent : Colors.Transparent;
        ManualCard.Stroke = _mode == HotspotMode.Manual ? accent : Colors.Transparent;
        PasswordSection.IsVisible = _mode == HotspotMode.Automatic;
        StartButton.Text = Loc.T(_mode == HotspotMode.Automatic ? "GroupStart" : "SetupOpenHotspotSettings");
        ValidatePassword();
    }

    private bool ValidatePassword()
    {
        if (_mode == HotspotMode.Manual)
        {
            StartButton.IsEnabled = true;
            return true;
        }

        var error = PasswordGenerator.Validate(PasswordEntry.Text);
        PasswordHint.Text = error ?? Loc.T("SetupPasswordInfo", PasswordEntry.Text.Length);
        PasswordHint.TextColor = error == null
            ? (Color)Application.Current!.Resources["WrTextMuted"]
            : (Color)Application.Current!.Resources["WrDanger"];
        StartButton.IsEnabled = error == null;
        return error == null;
    }

    private void OnAutoTapped(object? sender, TappedEventArgs e)
    {
        _mode = HotspotMode.Automatic;
        UpdateSelection();
    }

    private void OnManualTapped(object? sender, TappedEventArgs e)
    {
        _mode = HotspotMode.Manual;
        UpdateSelection();
    }

    private void OnPasswordChanged(object? sender, TextChangedEventArgs e) => ValidatePassword();

    private void OnRegenerateClicked(object? sender, EventArgs e) => RegeneratePassword();

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        if (!ValidatePassword())
            return;

        AppSettings.LastHotspotMode = (int)_mode;
        if (_mode == HotspotMode.Automatic)
            AppSettings.LastHotspotPassword = PasswordEntry.Text ?? string.Empty;

        _result.TrySetResult(new HotspotSetupResult(_mode, PasswordEntry.Text ?? string.Empty));
        await Navigation.PopModalAsync();
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
