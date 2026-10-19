using System.Text.RegularExpressions;

/// <summary>Masks credentials in text that is about to be logged (JSON bodies, URLs).</summary>
public static class LogRedactor
{
    private const string Mask = "***";

    private static readonly Regex JsonSecret = new(
        "(\"(?:password|passwordSalt|accessToken|refreshToken|token|OAuthToken)\"\\s*:\\s*\")(?:[^\"\\\\]|\\\\.)*(\")",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuerySecret = new(
        "([?&](?:token|accessToken|refreshToken)=)[^&\\s\"]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = JsonSecret.Replace(text, "$1" + Mask + "$2");
        return QuerySecret.Replace(text, "$1" + Mask);
    }
}
