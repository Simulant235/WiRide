using WiRide.Services;

namespace WiRide;

// XAML: Text="{app:Tr Schluessel}"
[ContentProperty(nameof(Key))]
public class TrExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = string.Empty;

    public string ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
