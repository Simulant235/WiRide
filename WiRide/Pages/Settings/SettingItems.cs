namespace WiRide.Pages;

// Beschreibung einer Einstellung; die Darstellung baut SettingsCategoryPage
public abstract class SettingItem
{
    public required string TitleKey { get; init; }
    public string? DescriptionKey { get; init; }
    public string Icon { get; init; } = WiRide.Icons.Tune;
    public bool Experimental { get; init; }

    // true = Oberflaeche wird danach neu aufgebaut (Sprache, Design, Schrift)
    public bool ReloadsUi { get; init; }
}

public sealed class ToggleSetting : SettingItem
{
    public required Func<bool> Get { get; init; }
    public required Action<bool> Set { get; init; }
}

public sealed class SliderSetting : SettingItem
{
    public double Min { get; init; }
    public double Max { get; init; } = 1;
    public required Func<double> Get { get; init; }
    public required Action<double> Set { get; init; }
    public Func<double, string> Format { get; init; } = v => $"{v * 100:0} %";
}

// Schalter mit Regler darunter (Regler nur sichtbar, wenn eingeschaltet)
public sealed class ToggleSliderSetting : SettingItem
{
    public required Func<bool> GetEnabled { get; init; }
    public required Action<bool> SetEnabled { get; init; }
    public required Func<double> GetValue { get; init; }
    public required Action<double> SetValue { get; init; }
    public string SliderLabelKey { get; init; } = "Strength";
}

public sealed class ChoiceSetting : SettingItem
{
    public required Func<IReadOnlyList<string>> Options { get; init; }
    public required Func<int> Get { get; init; }
    public required Action<int> Set { get; init; }
}

public sealed class TextSetting : SettingItem
{
    public required Func<string> Get { get; init; }

    // Rueckgabe: Fehlermeldung oder null wenn gespeichert
    public required Func<string, string?> Set { get; init; }
    public Keyboard Keyboard { get; init; } = Keyboard.Default;
    public string? PlaceholderKey { get; init; }
    public int MaxLength { get; init; } = 40;
}

public sealed class ActionSetting : SettingItem
{
    public required Func<Page, Task> Run { get; init; }
    public bool Danger { get; init; }
}

public sealed class InfoSetting : SettingItem
{
    public required Func<string> Value { get; init; }
}

public sealed record SettingsCategory(string TitleKey, string DescriptionKey, string Icon, Func<IEnumerable<SettingItem>> Items);
