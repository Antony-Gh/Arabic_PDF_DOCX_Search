namespace ArabicDocumentSearch.Core;

public sealed record TextQualityReport(
    int CharacterCount,
    int ArabicCharacterCount,
    int ReplacementCharacterCount,
    int ControlCharacterCount,
    bool PossibleMojibake,
    bool Suspicious);

public static class TextQualityAnalyzer
{
    private static readonly string[] MojibakeMarkers = ["Ã", "Â", "â", "Ø", "Ù", "�"];

    public static TextQualityReport Analyze(string text)
    {
        var arabic = text.Count(character => character is >= '\u0600' and <= '\u06FF');
        var replacements = text.Count(character => character == '\uFFFD');
        var controls = text.Count(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t');
        var mojibake = MojibakeMarkers.Any(text.Contains);
        return new TextQualityReport(text.Length, arabic, replacements, controls, mojibake, replacements > 0 || controls > 0 || mojibake);
    }
}
