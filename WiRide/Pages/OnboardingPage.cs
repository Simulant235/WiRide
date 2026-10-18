using WiRide.Services;

namespace WiRide.Pages;

// Kurze Einfuehrung beim ersten Start (auch spaeter ueber Einstellungen > Ueber abrufbar)
public class OnboardingPage : ContentPage
{
    private record Slide(string Icon, string TitleKey, string TextKey);

    private static readonly Slide[] Slides =
    {
        new(Icons.Motorcycle, "Onb1Title", "Onb1Text"),
        new(Icons.WifiTethering, "Onb2Title", "Onb2Text"),
        new(Icons.QrScanner, "Onb3Title", "Onb3Text"),
        new(Icons.Headset, "Onb4Title", "Onb4Text"),
    };

    private readonly CarouselView _carousel;
    private readonly Button _next;

    public OnboardingPage()
    {
        Style = SettingsUi.Style("WrPage");

        _carousel = new CarouselView
        {
            ItemsSource = Slides,
            Loop = false,
            ItemTemplate = new DataTemplate(() =>
            {
                var icon = new Label { Style = SettingsUi.Style("WrIcon"), FontSize = 72, TextColor = ThemeManager.Get("WrAccent") };
                icon.SetBinding(Label.TextProperty, nameof(Slide.Icon));

                var title = new Label { Style = SettingsUi.Style("WrTitle"), HorizontalTextAlignment = TextAlignment.Center };
                title.SetBinding(Label.TextProperty, new Binding(nameof(Slide.TitleKey), converter: new TrConverter()));

                var text = new Label { Style = SettingsUi.Style("WrBody"), HorizontalTextAlignment = TextAlignment.Center, TextColor = ThemeManager.Get("WrTextMuted") };
                text.SetBinding(Label.TextProperty, new Binding(nameof(Slide.TextKey), converter: new TrConverter()));

                return new VerticalStackLayout
                {
                    Spacing = 20,
                    Padding = new Thickness(28, 0),
                    VerticalOptions = LayoutOptions.Center,
                    Children = { icon, title, text }
                };
            })
        };

        var indicator = new IndicatorView
        {
            IndicatorColor = ThemeManager.Get("WrSurfaceHigh"),
            SelectedIndicatorColor = ThemeManager.Get("WrAccent"),
            HorizontalOptions = LayoutOptions.Center,
            IndicatorSize = 10
        };
        _carousel.IndicatorView = indicator;

        _next = new Button { Text = Loc.T("OnbNext"), Style = SettingsUi.Style("WrPrimaryButton") };
        _next.Clicked += OnNextClicked;

        var skip = new Button { Text = Loc.T("OnbSkip"), Style = SettingsUi.Style("WrSecondaryButton"), BackgroundColor = Colors.Transparent };
        skip.Clicked += async (_, _) => await FinishAsync();

        _carousel.PositionChanged += (_, e) =>
            _next.Text = Loc.T(e.CurrentPosition == Slides.Length - 1 ? "OnbStart" : "OnbNext");

        var root = new Grid
        {
            RowDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) },
            Padding = new Thickness(20, 24),
            RowSpacing = 12
        };
        root.Add(_carousel, 0, 0);
        root.Add(indicator, 0, 1);
        root.Add(_next, 0, 2);
        root.Add(skip, 0, 3);
        Content = root;
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        if (_carousel.Position < Slides.Length - 1)
            _carousel.Position++;
        else
            await FinishAsync();
    }

    private async Task FinishAsync()
    {
        AppSettings.OnboardingDone = true;
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        Dispatcher.Dispatch(async () => await FinishAsync());
        return true;
    }

    private class TrConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is string key ? Loc.T(key) : value;

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
