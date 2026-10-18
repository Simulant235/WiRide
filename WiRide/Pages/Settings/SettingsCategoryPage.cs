using WiRide.Services;

namespace WiRide.Pages;

// Zeigt die Einstellungen einer Kategorie alphabetisch sortiert; Aenderungen werden sofort gespeichert
public class SettingsCategoryPage : ContentPage
{
    private bool _loading = true;

    public SettingsCategoryPage(SettingsCategory category)
    {
        Style = SettingsUi.Style("WrPage");

        var list = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(0, 0, 0, 24) };
        foreach (var item in category.Items().OrderBy(i => Loc.T(i.TitleKey), StringComparer.Create(Loc.CurrentCulture, true)))
            list.Add(Build(item));

        var root = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
            Padding = new Thickness(20, 16)
        };
        root.Add(SettingsUi.Header(this, Loc.T(category.TitleKey)), 0, 0);
        root.Add(new ScrollView { Content = list }, 0, 1);
        Content = root;

        _loading = false;
    }

    private View Build(SettingItem item)
    {
        string title = Loc.T(item.TitleKey);
        string? description = item.DescriptionKey == null ? null : Loc.T(item.DescriptionKey);

        switch (item)
        {
            case ToggleSetting toggle:
            {
                var sw = new Switch { IsToggled = toggle.Get(), OnColor = ThemeManager.Get("WrAccent"), ThumbColor = Colors.White };
                sw.Toggled += (_, e) => Changed(item, () => toggle.Set(e.Value));
                return SettingsUi.Card(SettingsUi.Row(item.Icon, title, description, sw, item.Experimental));
            }

            case SliderSetting slider:
            {
                var value = new Label { Text = slider.Format(slider.Get()), Style = SettingsUi.Style("WrCaption"), VerticalOptions = LayoutOptions.Center };
                var control = new Slider(slider.Min, slider.Max, slider.Get())
                {
                    MinimumTrackColor = ThemeManager.Get("WrAccent"),
                    ThumbColor = ThemeManager.Get("WrAccent")
                };
                control.ValueChanged += (_, e) =>
                {
                    value.Text = slider.Format(e.NewValue);
                    Changed(item, () => slider.Set(e.NewValue));
                };
                return SettingsUi.Card(new VerticalStackLayout
                {
                    Spacing = 6,
                    Children = { SettingsUi.Row(item.Icon, title, description, value, item.Experimental), control }
                });
            }

            case ToggleSliderSetting toggleSlider:
            {
                var percent = new Label { Style = SettingsUi.Style("WrCaption"), VerticalOptions = LayoutOptions.Center };
                var slider = new Slider(0, 1, toggleSlider.GetValue())
                {
                    MinimumTrackColor = ThemeManager.Get("WrAccent"),
                    ThumbColor = ThemeManager.Get("WrAccent")
                };
                var sliderRow = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    ColumnSpacing = 10,
                    IsVisible = toggleSlider.GetEnabled()
                };
                sliderRow.Add(new Label { Text = Loc.T(toggleSlider.SliderLabelKey), Style = SettingsUi.Style("WrCaption"), VerticalOptions = LayoutOptions.Center }, 0);
                sliderRow.Add(slider, 1);
                sliderRow.Add(percent, 2);
                percent.Text = $"{slider.Value * 100:0} %";

                slider.ValueChanged += (_, e) =>
                {
                    percent.Text = $"{e.NewValue * 100:0} %";
                    Changed(item, () => toggleSlider.SetValue(e.NewValue));
                };

                var sw = new Switch { IsToggled = toggleSlider.GetEnabled(), OnColor = ThemeManager.Get("WrAccent"), ThumbColor = Colors.White };
                sw.Toggled += (_, e) =>
                {
                    sliderRow.IsVisible = e.Value;
                    Changed(item, () => toggleSlider.SetEnabled(e.Value));
                };

                return SettingsUi.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children = { SettingsUi.Row(item.Icon, title, description, sw, item.Experimental), sliderRow }
                });
            }

            case ChoiceSetting choice:
            {
                var picker = new Picker
                {
                    Style = SettingsUi.Style("WrPicker"),
                    ItemsSource = choice.Options().ToList(),
                    SelectedIndex = choice.Get()
                };
                picker.SelectedIndexChanged += (_, _) =>
                {
                    if (picker.SelectedIndex >= 0)
                        Changed(item, () => choice.Set(picker.SelectedIndex));
                };
                return SettingsUi.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children = { SettingsUi.Row(item.Icon, title, description, null, item.Experimental), picker }
                });
            }

            case TextSetting text:
            {
                var error = new Label { Style = SettingsUi.Style("WrCaption"), TextColor = ThemeManager.Get("WrDanger"), IsVisible = false };
                var entry = new Entry
                {
                    Style = SettingsUi.Style("WrEntry"),
                    Text = text.Get(),
                    Keyboard = text.Keyboard,
                    MaxLength = text.MaxLength,
                    Placeholder = text.PlaceholderKey == null ? null : Loc.T(text.PlaceholderKey)
                };

                void Save()
                {
                    var message = text.Set(entry.Text ?? string.Empty);
                    error.Text = message;
                    error.IsVisible = message != null;
                    if (message == null)
                        entry.Text = text.Get();
                }

                entry.Unfocused += (_, _) => Save();
                entry.Completed += (_, _) => Save();
                Disappearing += (_, _) => { if (entry.IsFocused) Save(); };

                return SettingsUi.Card(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children = { SettingsUi.Row(item.Icon, title, description, null, item.Experimental), entry, error }
                });
            }

            case ActionSetting action:
            {
                var row = SettingsUi.Row(item.Icon, title, description, SettingsUi.Icon(Icons.ChevronRight), item.Experimental,
                    action.Danger ? "WrDanger" : "WrText");

                var card = SettingsUi.Card(row);
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) => await action.Run(this);
                card.GestureRecognizers.Add(tap);
                return card;
            }

            case InfoSetting info:
            {
                var value = new Label { Text = info.Value(), Style = SettingsUi.Style("WrCaption"), VerticalOptions = LayoutOptions.Center };
                return SettingsUi.Card(SettingsUi.Row(item.Icon, title, description, value, item.Experimental));
            }

            default:
                return new Label { Text = title };
        }
    }

    private void Changed(SettingItem item, Action save)
    {
        if (_loading)
            return;

        save();

        if (item.AfterChange != null)
            Dispatcher.Dispatch(async () => await item.AfterChange(this));

        if (item.ReloadsUi)
            Dispatcher.Dispatch(async () => await ThemeManager.ReloadUiAsync(reopenSettings: true));
    }
}
