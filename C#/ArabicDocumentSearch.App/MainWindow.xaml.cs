using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using ArabicDocumentSearch.Core;

namespace ArabicDocumentSearch.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly SearchViewModel _viewModel;

    public MainWindow(SearchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == Forms.DialogResult.OK) _viewModel.SetRootFolder(dialog.SelectedPath);
    }

    private void ExcludeFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == Forms.DialogResult.OK) _viewModel.AddExcludedFolder(dialog.SelectedPath);
    }

    private void Query_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel.SearchCommand.CanExecute(null)) _viewModel.SearchCommand.Execute(null);
    }

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGrid grid && grid.SelectedItem is SearchResult result) _viewModel.OpenResultCommand.Execute(result);
    }
}