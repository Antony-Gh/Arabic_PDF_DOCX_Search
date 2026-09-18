using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ArabicDocumentSearch.Core;

namespace ArabicDocumentSearch.App;

public sealed class SearchViewModel : INotifyPropertyChanged
{
    private readonly IDocumentIndexer _indexer;
    private readonly ITextIndex _searchIndex;
    private CancellationTokenSource? _indexCancellation;
    private CancellationTokenSource? _searchCancellation;
    private string _rootFolder = string.Empty;
    private string _excludedFolders = string.Empty;
    private string _query = string.Empty;
    private string _status = "Choose a folder and index your documents.";
    private string _currentFile = string.Empty;
    private string _elapsed = "Elapsed: 00:00:00";
    private string _started = "Started: Calculating...";
    private string _remaining = "Remaining: Calculating...";
    private string _completion = "Estimated completion: Calculating...";
    private string _speed = "Speed: Calculating...";
    private double _progress;
    private bool _isBusy;

    public SearchViewModel(IDocumentIndexer indexer, ITextIndex searchIndex)
    {
        _indexer = indexer;
        _searchIndex = searchIndex;
        IndexCommand = new AsyncCommand(IndexAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(RootFolder));
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        SearchCommand = new RelayCommand(() => _ = SearchAsync(), () => !string.IsNullOrWhiteSpace(Query));
        OpenResultCommand = new RelayCommand<SearchResult>(OpenResult);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<SearchResult> Results { get; } = [];
    public ICommand IndexCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand OpenResultCommand { get; }

    public string RootFolder { get => _rootFolder; set { _rootFolder = value; OnChanged(); OnChanged(nameof(CanIndex)); RefreshCommands(); } }
    public string ExcludedFolders { get => _excludedFolders; set { _excludedFolders = value; OnChanged(); } }
    public string Query { get => _query; set { _query = value; OnChanged(); RefreshCommands(); } }
    public string Status { get => _status; private set { _status = value; OnChanged(); } }
    public string CurrentFile { get => _currentFile; private set { _currentFile = value; OnChanged(); } }
    public string Elapsed { get => _elapsed; private set { _elapsed = value; OnChanged(); } }
    public string Started { get => _started; private set { _started = value; OnChanged(); } }
    public string Remaining { get => _remaining; private set { _remaining = value; OnChanged(); } }
    public string Completion { get => _completion; private set { _completion = value; OnChanged(); } }
    public string Speed { get => _speed; private set { _speed = value; OnChanged(); } }
    public double Progress { get => _progress; private set { _progress = value; OnChanged(); } }
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnChanged(); OnChanged(nameof(CanIndex)); RefreshCommands(); } }
    public bool CanIndex => !IsBusy && !string.IsNullOrWhiteSpace(RootFolder);

    public void SetRootFolder(string path) => RootFolder = path;
    public void AddExcludedFolder(string path)
    {
        var folders = ExcludedFolders.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!folders.Contains(path, StringComparer.OrdinalIgnoreCase)) folders.Add(path);
        ExcludedFolders = string.Join(Environment.NewLine, folders);
    }

    private async Task IndexAsync()
    {
        IsBusy = true; Progress = 0; Results.Clear(); _indexCancellation = new CancellationTokenSource();
        try
        {
            var excluded = ExcludedFolders.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var progress = new ThrottledProgress(new Progress<IndexProgress>(UpdateProgress), TimeSpan.FromMilliseconds(150));
            var summary = await Task.Run(
                () => _indexer.IndexAsync(RootFolder, new IndexOptions(true, excluded), progress, _indexCancellation.Token),
                _indexCancellation.Token);
            UpdateProgress(summary);
            Status = $"Ready. Indexed {summary.Indexed}, skipped {summary.Skipped}, failed {summary.Errors} of {summary.Total} files.";
        }
        catch (OperationCanceledException) { Status = "Indexing cancelled."; }
        catch (Exception exception) { Status = $"Indexing failed: {exception.Message}"; }
        finally { _indexCancellation.Dispose(); _indexCancellation = null; IsBusy = false; }
    }

    private void UpdateProgress(IndexProgress progress)
    {
        Progress = progress.Total == 0 ? 100 : progress.Processed * 100d / progress.Total;
        CurrentFile = progress.CurrentFile ?? string.Empty;
        Started = progress.StartedAtUtc == default
            ? "Started: Calculating..."
            : $"Started: {progress.StartedAtUtc.ToLocalTime():HH:mm:ss}";
        Elapsed = $"Elapsed: {progress.Elapsed:hh\\:mm\\:ss}";
        Remaining = progress.EstimatedRemaining is { } remaining
            ? $"Remaining: {remaining:hh\\:mm\\:ss}"
            : "Remaining: Calculating...";
        Completion = progress.EstimatedCompletionUtc is { } completion
            ? $"Estimated completion: {completion.ToLocalTime():HH:mm:ss}"
            : "Estimated completion: Calculating...";
        Speed = progress.FilesPerSecond > 0
            ? $"Speed: {progress.FilesPerSecond:N1} files/sec"
            : "Speed: Calculating...";
        Status = $"Indexing {progress.Processed:N0}/{progress.Total:N0}  |  Indexed {progress.Indexed:N0}  |  Skipped {progress.Skipped:N0}  |  Errors {progress.Errors:N0}";
    }

    private void Cancel() => _indexCancellation?.Cancel();
    private async Task SearchAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _searchCancellation = cancellation;
        var stopwatch = Stopwatch.StartNew();
        Results.Clear();
        try
        {
            var outcome = await _searchIndex.SearchAsync(Query, 500, cancellation.Token);
            if (outcome.Cancelled)
            {
                Status = "Search cancelled.";
                return;
            }
            if (!outcome.Succeeded)
            {
                Status = $"Search failed: {outcome.ErrorMessage}";
                return;
            }
            foreach (var result in outcome.Results) Results.Add(result);
            Status = outcome.Results.Count == 0
                ? $"No results • {stopwatch.ElapsedMilliseconds:N0} ms"
                : $"Found {outcome.Results.Count:N0} results • {stopwatch.ElapsedMilliseconds:N0} ms";
        }
        catch (OperationCanceledException)
        {
            Status = "Search cancelled.";
        }
        catch (Exception exception)
        {
            Status = $"Search failed: {exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation)) _searchCancellation = null;
            cancellation.Dispose();
        }
    }

    private static void OpenResult(SearchResult? result)
    {
        if (result is null) return;
        Process.Start(new ProcessStartInfo(result.FullPath) { UseShellExecute = true });
    }

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void RefreshCommands()
    {
        if (IndexCommand is AsyncCommand index) index.RaiseCanExecuteChanged();
        if (CancelCommand is RelayCommand cancel) cancel.RaiseCanExecuteChanged();
        if (SearchCommand is RelayCommand search) search.RaiseCanExecuteChanged();
    }
}

public sealed class ThrottledProgress(IProgress<IndexProgress> inner, TimeSpan interval) : IProgress<IndexProgress>
{
    private readonly object _gate = new();
    private DateTime _lastReportUtc = DateTime.MinValue;

    public void Report(IndexProgress value)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            if (now - _lastReportUtc < interval && value.Processed < value.Total) return;
            _lastReportUtc = now;
            inner.Report(value);
        }
    }
}

public sealed class RelayCommand(Action action, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => action();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T>(Action<T?> action) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action((T?)parameter);
}

public sealed class AsyncCommand(Func<Task> action, Func<bool> canExecute) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running && canExecute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public async void Execute(object? parameter)
    {
        if (_running) return;
        _running = true; RaiseCanExecuteChanged();
        try { await action(); } finally { _running = false; RaiseCanExecuteChanged(); }
    }
}
