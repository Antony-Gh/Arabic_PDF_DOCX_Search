using System.Text;
using System.Text.RegularExpressions;

namespace ArabicDocumentSearch.Core;

public sealed partial class ArabicTextNormalizer : IArabicTextNormalizer
{
    [GeneratedRegex(@"[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED\u0640]")]
    private static partial Regex ArabicMarks();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = ArabicMarks().Replace(text, string.Empty)
            .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ٱ', 'ا')
            .Replace('ى', 'ي').Replace('ة', 'ه');
        var digits = "٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹";
        var ascii = "01234567890123456789";
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            var index = digits.IndexOf(character);
            builder.Append(index >= 0 ? ascii[index] : character);
        }
        return Whitespace().Replace(builder.ToString(), " ").Trim().ToLowerInvariant();
    }
}
