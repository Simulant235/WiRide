using System.Security.Cryptography;

namespace WiRide.Services;

public enum PasswordCharset
{
    LettersAndDigits,
    DigitsOnly,
    LettersOnly
}

public static class PasswordGenerator
{
    // WPA2 schreibt mindestens 8 Zeichen vor
    public const int MinLength = 8;
    public const int MaxLength = 63;
    public const int DefaultLength = 8;

    // Ohne leicht verwechselbare Zeichen (0/O, 1/l/I)
    private const string Letters = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";

    public static string Generate(PasswordCharset charset, int length = DefaultLength)
    {
        var chars = charset switch
        {
            PasswordCharset.DigitsOnly => "0123456789",
            PasswordCharset.LettersOnly => Letters,
            _ => Letters + Digits
        };

        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)])
            .ToArray());
    }

    // null = gueltig, sonst Fehlermeldung
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return Loc.T("PwTooShort", MinLength);
        if (password.Length > MaxLength)
            return Loc.T("PwTooLong", MaxLength);
        if (!password.All(char.IsAsciiLetterOrDigit))
            return Loc.T("PwInvalidChars");
        return null;
    }
}
