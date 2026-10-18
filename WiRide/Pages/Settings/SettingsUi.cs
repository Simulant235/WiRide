using WiRide.Services;

namespace WiRide.Pages;

// Bausteine fuer die per Code erstellten Einstellungsseiten
public static class SettingsUi
{
    public static Style Style(string key) => (Style)Application.Current!.Resources[key];

    public static Grid Header(Page page, string title)
    {
        var back = new Button { Text = Icons.ArrowBack, Style = Style("WrIconButton") };
        back.Clicked += async (_, _) => await page.Navigation.PopAsync();

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12,
            Margin = new Thickness(0, 8, 0, 12)
        };
        grid.Add(back, 0);
        grid.Add(new Label { Text = title, Style = Style("WrTitle") }, 1);
        return grid;
    }

    public static Border Card(View content, Thickness? padding = null) => new()
    {
        Style = Style("WrCard"),
        Padding = padding ?? new Thickness(16, 14),
        Content = content
    };

    public static Label Icon(string glyph, string colorKey = "WrTextMuted", double size = 22) => new()
    {
        Text = glyph,
        Style = Style("WrIcon"),
        FontSize = size,
        TextColor = ThemeManager.Get(colorKey)
    };

    // Zeile: Icon | Titel + Beschreibung | rechts optionales Element
    public static Grid Row(string glyph, string title, string? description, View? trailing, bool experimental = false, string titleColorKey = "WrText")
    {
        var texts = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };

        var titleRow = new HorizontalStackLayout { Spacing = 8 };
        titleRow.Add(new Label { Text = title, Style = Style("WrBody"), TextColor = ThemeManager.Get(titleColorKey) });
        if (experimental)
            titleRow.Add(Badge(Loc.T("Experimental")));
        texts.Add(titleRow);

        if (!string.IsNullOrEmpty(description))
            texts.Add(new Label { Text = description, Style = Style("WrCaption") });

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 14
        };
        grid.Add(Icon(glyph), 0);
        grid.Add(texts, 1);
        if (trailing != null)
            grid.Add(trailing, 2);
        return grid;
    }

    public static Border Badge(string text) => new()
    {
        BackgroundColor = ThemeManager.Get("WrAccentSoft"),
        StrokeThickness = 0,
        Padding = new Thickness(8, 2),
        VerticalOptions = LayoutOptions.Center,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
        Content = new Label
        {
            Text = text.ToUpperInvariant(),
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = ThemeManager.Get("WrAccent")
        }
    };
}
