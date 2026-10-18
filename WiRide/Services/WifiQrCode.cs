using System.Text;

namespace WiRide.Services;

// Standard-WLAN-QR-Format: WIFI:T:WPA;S:<ssid>;P:<passwort>;;
// Wird auch von der normalen Handy-Kamera erkannt.
public static class WifiQrCode
{
    public static string Build(string ssid, string password) =>
        $"WIFI:T:WPA;S:{Escape(ssid)};P:{Escape(password)};;";

    public static bool TryParse(string? text, out string ssid, out string password)
    {
        ssid = string.Empty;
        password = string.Empty;

        if (text == null || !text.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var (key, value) in SplitFields(text[5..]))
        {
            if (key == "S")
                ssid = value;
            else if (key == "P")
                password = value;
        }

        return ssid.Length > 0;
    }

    private static string Escape(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
        {
            if (c is '\\' or ';' or ',' or ':' or '"')
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static IEnumerable<(string Key, string Value)> SplitFields(string body)
    {
        var current = new StringBuilder();
        bool escaped = false;

        foreach (var c in body)
        {
            if (escaped)
            {
                current.Append(c);
                escaped = false;
            }
            else if (c == '\\')
            {
                escaped = true;
            }
            else if (c == ';')
            {
                var field = current.ToString();
                current.Clear();
                int colon = field.IndexOf(':');
                if (colon > 0)
                    yield return (field[..colon].ToUpperInvariant(), field[(colon + 1)..]);
            }
            else
            {
                // Nur den ersten Doppelpunkt als Trenner behandeln, der Rest ist Wert
                current.Append(c);
            }
        }
    }
}
