using System.Globalization;
using System.Text;
using WiRide.Services;

namespace WiRide.Pages;

// Feedback, Fehlermeldung oder Uebersetzungs-Verbesserung per E-Mail an den Entwickler.
// Diagnosedaten und Standort werden nur mit Zustimmung angehaengt (Datenschutz).
public class FeedbackPage : ContentPage
{
    // Empfaenger-Adresse fuer Feedback (leer = noch nicht eingerichtet)
    public const string FeedbackEmail = "";

    private static readonly string[] Categories = { "FbFeedback", "FbBug", "FbTranslation" };

    private readonly Picker _category;
    private readonly Editor _message;
    private readonly Editor _bugDetails;
    private readonly VerticalStackLayout _bugSection;
    private readonly CheckBox _diagnostics;
    private readonly CheckBox _location;

    public FeedbackPage()
    {
        Style = SettingsUi.Style("WrPage");

        _category = new Picker { Style = SettingsUi.Style("WrPicker"), ItemsSource = Categories.Select(Loc.T).ToList(), SelectedIndex = 0 };
        _message = Editor(Loc.T("FbMessagePlaceholder"), 140);
        _bugDetails = Editor(Loc.T("FbBugPlaceholder"), 120);
        _bugSection = new VerticalStackLayout
        {
            Spacing = 6,
            IsVisible = false,
            Children = { new Label { Text = Loc.T("FbBugTitle"), Style = SettingsUi.Style("WrCaption") }, _bugDetails }
        };
        _category.SelectedIndexChanged += (_, _) => _bugSection.IsVisible = _category.SelectedIndex == 1;

        _diagnostics = new CheckBox { IsChecked = true, Color = ThemeManager.Get("WrAccent") };
        _location = new CheckBox { IsChecked = false, Color = ThemeManager.Get("WrAccent") };

        var send = new Button { Text = Loc.T("FbSend"), Style = SettingsUi.Style("WrPrimaryButton") };
        send.Clicked += OnSendClicked;

        var content = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(0, 0, 0, 24),
            Children =
            {
                new Label { Text = Loc.T("FbIntro"), Style = SettingsUi.Style("WrCaption") },
                SettingsUi.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        new Label { Text = Loc.T("FbCategory"), Style = SettingsUi.Style("WrCaption") }, _category,
                        new Label { Text = Loc.T("FbMessage"), Style = SettingsUi.Style("WrCaption") }, _message,
                        _bugSection
                    }
                }),
                SettingsUi.Card(new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        CheckRow(_diagnostics, Loc.T("FbDiagnostics"), Loc.T("FbDiagnosticsDesc")),
                        CheckRow(_location, Loc.T("FbLocation"), Loc.T("FbLocationDesc"))
                    }
                }),
                send
            }
        };

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            Padding = new Thickness(20, 16)
        };
        root.Add(SettingsUi.Header(this, Loc.T("SetFeedback")), 0, 0);
        root.Add(new ScrollView { Content = content }, 0, 1);
        Content = root;
    }

    private static Editor Editor(string placeholder, double height) => new()
    {
        Placeholder = placeholder,
        HeightRequest = height,
        AutoSize = EditorAutoSizeOption.TextChanges,
        TextColor = ThemeManager.Get("WrText"),
        PlaceholderColor = ThemeManager.Get("WrTextMuted"),
        BackgroundColor = ThemeManager.Get("WrSurfaceHigh")
    };

    private static Grid CheckRow(CheckBox box, string title, string description)
    {
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 8 };
        grid.Add(box, 0);
        grid.Add(new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = title, Style = SettingsUi.Style("WrBody") },
                new Label { Text = description, Style = SettingsUi.Style("WrCaption") }
            }
        }, 1);
        return grid;
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_message.Text))
        {
            await DisplayAlertAsync(Loc.T("SetFeedback"), Loc.T("FbEmpty"), Loc.T("Ok"));
            return;
        }

        if (string.IsNullOrEmpty(FeedbackEmail))
        {
            await DisplayAlertAsync(Loc.T("SetFeedback"), Loc.T("FbNoAddress"), Loc.T("Ok"));
            return;
        }

        var category = Loc.T(Categories[Math.Max(0, _category.SelectedIndex)]);
        var body = new StringBuilder();
        body.AppendLine(_message.Text.Trim());

        if (_bugSection.IsVisible && !string.IsNullOrWhiteSpace(_bugDetails.Text))
        {
            body.AppendLine().AppendLine("--- Bug ---").AppendLine(_bugDetails.Text.Trim());
        }

        if (_diagnostics.IsChecked)
        {
            body.AppendLine().AppendLine("--- Diagnose ---");
            body.AppendLine($"Rider: {AppSettings.RiderName}");
            body.AppendLine($"App-ID: {AppSettings.InstallId}");
            body.AppendLine($"WiRide: {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})");
            body.AppendLine($"Geraet: {DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}");
            body.AppendLine($"System: {DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}");
            body.AppendLine($"Sprache: {Loc.CurrentCode} · Land: {RegionInfo.CurrentRegion.TwoLetterISORegionName}");
            body.AppendLine($"Latenz-Modus: {(AppSettings.LowLatency ? "low" : "compat")} · Opus: {AppSettings.UseOpus} · {AppSettings.SampleRate} Hz");
        }

        if (_location.IsChecked)
        {
            try
            {
                var location = await Geolocation.Default.GetLastKnownLocationAsync()
                               ?? await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(5)));
                if (location != null)
                    body.AppendLine($"Standort (ca. 10 km): {Math.Round(location.Latitude, 1)}, {Math.Round(location.Longitude, 1)}");
            }
            catch { }
        }

        try
        {
            await Email.Default.ComposeAsync(new EmailMessage
            {
                Subject = $"WiRide {category}",
                Body = body.ToString(),
                To = new List<string> { FeedbackEmail }
            });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(Loc.T("SetFeedback"), Loc.T("FbNoMailApp", ex.Message), Loc.T("Ok"));
        }
    }
}
