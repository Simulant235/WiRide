using WiRide.Services;

namespace WiRide.Pages;

// Uebersicht der Einstellungs-Kategorien; jede Kategorie oeffnet eine eigene Seite
public class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        Style = SettingsUi.Style("WrPage");

        var list = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(0, 0, 0, 24) };

        foreach (var category in SettingsCatalog.Categories)
        {
            var row = SettingsUi.Row(category.Icon, Loc.T(category.TitleKey), Loc.T(category.DescriptionKey),
                SettingsUi.Icon(Icons.ChevronRight));
            ((Label)row.Children[0]).TextColor = ThemeManager.Get("WrAccent");

            var card = SettingsUi.Card(row, new Thickness(16, 16));
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await Navigation.PushAsync(new SettingsCategoryPage(category));
            card.GestureRecognizers.Add(tap);
            list.Add(card);
        }

        list.Add(new Label
        {
            Text = $"WiRide {AppInfo.Current.VersionString}",
            Style = SettingsUi.Style("WrCaption"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 12, 0, 0)
        });

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            Padding = new Thickness(20, 16)
        };
        root.Add(SettingsUi.Header(this, Loc.T("SettingsTitle")), 0, 0);
        root.Add(new ScrollView { Content = list }, 0, 1);
        Content = root;
    }
}
