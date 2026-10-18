using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace WiRide.Services;

public record LanguageInfo(string Code, string NativeName);

// Einfache Lokalisierung ueber JSON-Dateien (Resources/Strings/<code>.json).
// Neue Sprache = neue JSON-Datei mit denselben Schluesseln.
public static class Loc
{
    public static readonly LanguageInfo[] Languages =
    {
        new("ar", "العربية"), new("cs", "Čeština"), new("da", "Dansk"), new("de", "Deutsch"),
        new("el", "Ελληνικά"), new("en", "English"), new("es", "Español"), new("fi", "Suomi"),
        new("fil", "Filipino"), new("fr", "Français"), new("hi", "हिन्दी"), new("id", "Bahasa Indonesia"),
        new("it", "Italiano"), new("ja", "日本語"), new("ko", "한국어"), new("nl", "Nederlands"),
        new("no", "Norsk"), new("pl", "Polski"), new("pt", "Português"), new("ro", "Română"),
        new("ru", "Русский"), new("sv", "Svenska"), new("th", "ไทย"), new("tr", "Türkçe"),
        new("uk", "Українська"), new("vi", "Tiếng Việt"), new("zh", "中文")
    };

    private static Dictionary<string, string> _current = new();
    private static Dictionary<string, string> _fallback = new();

    public static string CurrentCode { get; private set; } = "en";
    public static CultureInfo CurrentCulture => new(CurrentCode == "no" ? "nb" : CurrentCode);

    // Nur Sprachen mit vorhandener Datei
    public static IEnumerable<LanguageInfo> AvailableLanguages => Languages.Where(l => Read(l.Code) != null);

    public static void Initialize()
    {
        var code = AppSettings.Language;
        if (code == AppSettings.SystemLanguage)
            code = MapSystemLanguage(CultureInfo.CurrentUICulture);

        _fallback = Read("en") ?? new();
        _current = Read(code) ?? _fallback;
        CurrentCode = _current == _fallback ? "en" : code;
    }

    public static string T(string key) =>
        _current.TryGetValue(key, out var value) || _fallback.TryGetValue(key, out value) ? value : key;

    public static string T(string key, params object?[] args) => string.Format(T(key), args);

    private static string MapSystemLanguage(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "nb" or "nn" => "no",
        "tl" => "fil",
        var code => code
    };

    private static readonly Dictionary<string, Dictionary<string, string>?> Cache = new();

    private static Dictionary<string, string>? Read(string code)
    {
        if (Cache.TryGetValue(code, out var cached))
            return cached;

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Strings.{code}.json");
        var result = stream == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        Cache[code] = result;
        return result;
    }
}
