using System.Security.Cryptography;
using System.Text;
using ArabicDocumentSearch.Core;

namespace ArabicDocumentSearch.Infrastructure;

public sealed class FileDiscovery
{
    private static readonly HashSet<string> Extensions = [".pdf", ".docx"];

    public IEnumerable<DiscoveredDocument> Discover(string rootFolder, IReadOnlyCollection<string> excludedFolders, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(rootFolder);
        var excluded = excludedFolders.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            if (excluded.Contains(current)) continue;
            string[] directories;
            string[] files;
            try
            {
                directories = Directory.GetDirectories(current);
                files = Directory.GetFiles(current);
            }
            catch { continue; }
            foreach (var directory in directories)
            {
                if (!excluded.Contains(Path.GetFullPath(directory))) pending.Push(directory);
            }
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (!Extensions.Contains(extension)) continue;
                FileInfo info;
                try { info = new FileInfo(file); } catch { continue; }
                var id = CreateId(file, info.Length, info.LastWriteTimeUtc);
                yield return new DiscoveredDocument(Path.GetFullPath(file), info.Name, extension, info.Length, info.LastWriteTimeUtc, id);
            }
        }
    }

    private static string CreateId(string path, long size, DateTime lastWriteTimeUtc)
    {
        var input = $"{Path.GetFullPath(path).ToUpperInvariant()}|{size}|{lastWriteTimeUtc.Ticks}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
