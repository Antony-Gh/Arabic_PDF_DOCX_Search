using System.IO;
using System.Windows;

namespace ArabicDocumentSearch.App;

public partial class DiagnosticsWindow : Window
{
    private readonly string _path;

    public DiagnosticsWindow(string path)
    {
        InitializeComponent();
        _path = path;
        RefreshLog();
    }

    private void RefreshLog()
    {
        var files = Directory.Exists(_path) ? Directory.GetFiles(_path, "*.jsonl") : [];
        LogText.Text = files.Length == 0
            ? "No diagnostics have been recorded yet."
            : string.Join(Environment.NewLine + Environment.NewLine, files.Select(file => $"===== {Path.GetFileName(file)} =====\n{File.ReadAllText(file)}"));
    }
    private void Copy_Click(object sender, RoutedEventArgs e) => System.Windows.Clipboard.SetText(LogText.Text);
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshLog();

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Text files (*.txt)|*.txt", FileName = "search-diagnostics.txt" };
        if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, LogText.Text);
    }
}
