namespace WiRide.Services;

public enum ThemeChoice
{
    System,
    Dark,
    Light
}

// Wendet Design (hell/dunkel) und Schriftgroesse zur Laufzeit an
public static class ThemeManager
{
    private static readonly Dictionary<string, Color> Dark = new()
    {
        ["WrBackground"] = Color.FromArgb("#0F1115"),
        ["WrSurface"] = Color.FromArgb("#1A1D23"),
        ["WrSurfaceHigh"] = Color.FromArgb("#262A33"),
        ["WrAccent"] = Color.FromArgb("#FF8A00"),
        ["WrAccentSoft"] = Color.FromArgb("#33FF8A00"),
        ["WrSuccess"] = Color.FromArgb("#32D74B"),
        ["WrSuccessSoft"] = Color.FromArgb("#3332D74B"),
        ["WrDanger"] = Color.FromArgb("#FF453A"),
        ["WrText"] = Color.FromArgb("#F5F6F7"),
        ["WrTextMuted"] = Color.FromArgb("#9AA0A8"),
        ["WrOnAccent"] = Color.FromArgb("#141414"),
    };

    private static readonly Dictionary<string, Color> Light = new()
    {
        ["WrBackground"] = Color.FromArgb("#F3F4F6"),
        ["WrSurface"] = Color.FromArgb("#FFFFFF"),
        ["WrSurfaceHigh"] = Color.FromArgb("#E6E8EC"),
        ["WrAccent"] = Color.FromArgb("#F07C00"),
        ["WrAccentSoft"] = Color.FromArgb("#26F07C00"),
        ["WrSuccess"] = Color.FromArgb("#1F9D3A"),
        ["WrSuccessSoft"] = Color.FromArgb("#261F9D3A"),
        ["WrDanger"] = Color.FromArgb("#D93025"),
        ["WrText"] = Color.FromArgb("#14161A"),
        ["WrTextMuted"] = Color.FromArgb("#5C636E"),
        ["WrOnAccent"] = Color.FromArgb("#141414"),
    };

    private static readonly Dictionary<string, double> BaseFontSizes = new()
    {
        ["WrFontTitle"] = 24,
        ["WrFontLarge"] = 19,
        ["WrFontBody"] = 16,
        ["WrFontButton"] = 17,
        ["WrFontCaption"] = 13,
    };

    public static readonly double[] FontScales = { 0.9, 1.0, 1.15, 1.3 };

    public static bool IsDark { get; private set; } = true;

    public static void Apply()
    {
        var app = Application.Current;
        if (app == null)
            return;

        IsDark = AppSettings.Theme switch
        {
            ThemeChoice.Dark => true,
            ThemeChoice.Light => false,
            _ => app.PlatformAppTheme != AppTheme.Light
        };

        app.UserAppTheme = IsDark ? AppTheme.Dark : AppTheme.Light;

        foreach (var (key, color) in IsDark ? Dark : Light)
            app.Resources[key] = color;

        var scale = FontScales[Math.Clamp(AppSettings.FontScaleIndex, 0, FontScales.Length - 1)];
        foreach (var (key, size) in BaseFontSizes)
            app.Resources[key] = Math.Round(size * scale);
    }

    public static Color Get(string key) => (Color)Application.Current!.Resources[key];

    // Sprache/Design neu laden: Oberflaeche komplett neu aufbauen
    public static async Task ReloadUiAsync(bool reopenSettings)
    {
        Loc.Initialize();
        Apply();

        var window = Application.Current?.Windows.FirstOrDefault();
        if (window == null)
            return;

        var shell = new AppShell();
        window.Page = shell;

        if (reopenSettings)
            await shell.Navigation.PushAsync(new Pages.SettingsPage(), false);
    }
}
