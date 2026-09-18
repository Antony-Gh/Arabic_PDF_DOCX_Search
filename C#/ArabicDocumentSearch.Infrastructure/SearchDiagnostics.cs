using System.Text.Json;
using ArabicDocumentSearch.Core;

namespace ArabicDocumentSearch.Infrastructure;

public sealed class SearchDiagnostics(string logPath)
{
    private readonly object _gate = new();

    public void Write(SearchOutcome outcome, string? indexPath = null)
    {
        try
        {
            var entry = new
            {
                TimestampUtc = DateTime.UtcNow,
                Query = outcome.Query,
                Succeeded = outcome.Succeeded,
                Cancelled = outcome.Cancelled,
                ResultCount = outcome.Results.Count,
                ElapsedMilliseconds = outcome.Elapsed.TotalMilliseconds,
                ErrorType = outcome.ErrorType,
                ErrorMessage = outcome.ErrorMessage,
                ErrorStackTrace = outcome.ErrorStackTrace,
                IndexPath = indexPath,
                ApplicationVersion = typeof(SearchDiagnostics).Assembly.GetName().Version?.ToString()
            };
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, JsonSerializer.Serialize(entry) + Environment.NewLine);
            }
        }
        catch { }
    }
}
