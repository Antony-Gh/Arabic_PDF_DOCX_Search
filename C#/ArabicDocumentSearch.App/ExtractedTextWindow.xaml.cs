using System.IO;
using System.Text;
using System.Windows;
using ArabicDocumentSearch.Core;

namespace ArabicDocumentSearch.App;

public partial class ExtractedTextWindow : Window
{
    private readonly ExtractedDocument _document;

    public ExtractedTextWindow(ExtractedDocument document)
    {
        InitializeComponent();
        _document = document;
        DataContext = new ExtractedTextViewModel(document);
    }

    private void CopyOriginal_Click(object sender, RoutedEventArgs e) => System.Windows.Clipboard.SetText(((ExtractedTextViewModel)DataContext).OriginalText);
    private void CopyNormalized_Click(object sender, RoutedEventArgs e) => System.Windows.Clipboard.SetText(((ExtractedTextViewModel)DataContext).NormalizedText);

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Text files (*.txt)|*.txt", FileName = Path.GetFileNameWithoutExtension(_document.FileName) + "-extracted.txt" };
        if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, ((ExtractedTextViewModel)DataContext).OriginalText, Encoding.UTF8);
    }
}

public sealed class ExtractedTextViewModel(ExtractedDocument document)
{
    public string Header => $"{document.FileName}  |  {document.FullPath}";
    public string Quality
    {
        get
        {
            var report = TextQualityAnalyzer.Analyze(OriginalText);
            var warning = report.Suspicious ? "  Warning: possible extraction/encoding anomaly." : string.Empty;
            return $"Characters: {report.CharacterCount:N0}  Arabic: {report.ArabicCharacterCount:N0}  Replacement: {report.ReplacementCharacterCount:N0}  Controls: {report.ControlCharacterCount:N0}{warning}";
        }
    }
    public string OriginalText => string.Join(Environment.NewLine + Environment.NewLine, document.Pages.Select(page => $"[{page.Location}]\n{page.OriginalText}"));
    public string NormalizedText => string.Join(Environment.NewLine + Environment.NewLine, document.Pages.Select(page => $"[{page.Location}]\n{page.NormalizedText}"));
}
