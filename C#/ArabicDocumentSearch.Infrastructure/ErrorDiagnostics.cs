using System.Text.Json;

namespace ArabicDocumentSearch.Infrastructure;

public sealed class ErrorDiagnostics(string logPath)
{
    private readonly object _gate = new();

    public void Write(string operation, string? filePath, Exception exception, int? page = null, string severity = "Error")
    {
        try
        {
            var entry = new
            {
                TimestampUtc = DateTime.UtcNow,
                Severity = severity,
                Operation = operation,
                FilePath = filePath,
                Page = page,
                ExceptionType = exception.GetType().FullName,
                Message = exception.Message,
                StackTrace = exception.ToString(),
                ApplicationVersion = typeof(ErrorDiagnostics).Assembly.GetName().Version?.ToString()
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
