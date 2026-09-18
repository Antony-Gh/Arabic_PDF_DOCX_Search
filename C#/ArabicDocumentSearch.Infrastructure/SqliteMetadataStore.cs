using ArabicDocumentSearch.Core;
using Microsoft.Data.Sqlite;

namespace ArabicDocumentSearch.Infrastructure;

public sealed class SqliteMetadataStore(string databasePath) : IMetadataStore
{
    private readonly string _connectionString = $"Data Source={databasePath}";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Documents (
                Id TEXT PRIMARY KEY, FullPath TEXT NOT NULL UNIQUE, FileName TEXT NOT NULL,
                Extension TEXT NOT NULL, FileSize INTEGER NOT NULL, LastWriteTimeUtc TEXT NOT NULL,
                IndexedAtUtc TEXT, Status TEXT NOT NULL, PageCount INTEGER NOT NULL DEFAULT 0,
                HasText INTEGER NOT NULL DEFAULT 0, RequiresOcr INTEGER NOT NULL DEFAULT 0,
                OcrCompleted INTEGER NOT NULL DEFAULT 0, ErrorMessage TEXT);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DocumentMetadata?> GetAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,FullPath,FileName,Extension,FileSize,LastWriteTimeUtc,Status,IndexedAtUtc,PageCount,HasText,RequiresOcr,OcrCompleted,ErrorMessage FROM Documents WHERE Id=$id";
        command.Parameters.AddWithValue("$id", documentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new DocumentMetadata(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), DateTime.Parse(reader.GetString(5)), reader.GetString(6), reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)), reader.GetInt32(8), reader.GetInt32(9) != 0, reader.GetInt32(10) != 0, reader.GetInt32(11) != 0, reader.IsDBNull(12) ? null : reader.GetString(12));
    }

    public async Task UpsertAsync(DocumentMetadata metadata, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Documents (Id,FullPath,FileName,Extension,FileSize,LastWriteTimeUtc,IndexedAtUtc,Status,PageCount,HasText,RequiresOcr,OcrCompleted,ErrorMessage)
            VALUES ($id,$path,$name,$ext,$size,$last,$indexed,$status,$pages,$text,$ocr,$ocrDone,$error)
            ON CONFLICT(Id) DO UPDATE SET FullPath=$path,FileName=$name,Extension=$ext,FileSize=$size,LastWriteTimeUtc=$last,IndexedAtUtc=$indexed,Status=$status,PageCount=$pages,HasText=$text,RequiresOcr=$ocr,OcrCompleted=$ocrDone,ErrorMessage=$error;
            """;
        command.Parameters.AddWithValue("$id", metadata.Id); command.Parameters.AddWithValue("$path", metadata.FullPath); command.Parameters.AddWithValue("$name", metadata.FileName); command.Parameters.AddWithValue("$ext", metadata.Extension); command.Parameters.AddWithValue("$size", metadata.FileSize); command.Parameters.AddWithValue("$last", metadata.LastWriteTimeUtc.ToString("O")); command.Parameters.AddWithValue("$indexed", (object?)metadata.IndexedAtUtc?.ToString("O") ?? DBNull.Value); command.Parameters.AddWithValue("$status", metadata.Status); command.Parameters.AddWithValue("$pages", metadata.PageCount); command.Parameters.AddWithValue("$text", metadata.HasText ? 1 : 0); command.Parameters.AddWithValue("$ocr", metadata.RequiresOcr ? 1 : 0); command.Parameters.AddWithValue("$ocrDone", metadata.OcrCompleted ? 1 : 0); command.Parameters.AddWithValue("$error", (object?)metadata.ErrorMessage ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoveMissingAsync(IReadOnlySet<string> discoveredIds, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Documents";
        var existing = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken)) while (await reader.ReadAsync(cancellationToken)) existing.Add(reader.GetString(0));
        foreach (var id in existing.Where(id => !discoveredIds.Contains(id)))
        {
            command = connection.CreateCommand(); command.CommandText = "DELETE FROM Documents WHERE Id=$id"; command.Parameters.AddWithValue("$id", id); await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
