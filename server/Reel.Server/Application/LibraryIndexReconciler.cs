namespace Reel.Server.Application;

using Reel.Server.Application.Ports;
using Reel.Server.Domain;

public sealed class LibraryIndexReconciler
{
    private readonly ILibraryScanner _scanner;
    private readonly IMediaStore _mediaStore;

    public LibraryIndexReconciler(ILibraryScanner scanner, IMediaStore mediaStore)
    {
        _scanner = scanner;
        _mediaStore = mediaStore;
    }

    public async Task<int> ReconcileAsync(CancellationToken ct)
    {
        var scan = await _scanner.ScanAsync(ct);
        if (!scan.IsComplete)
            throw new IOException("Library scan was incomplete; the existing index was preserved.");

        var media = EnumerateFiles(scan.Root)
            .Select(node => new MediaRecord(node.RelativePath, node.ParsedTitle, node.Year))
            .ToArray();
        await _mediaStore.ReconcileAsync(media, ct);
        return media.Length;
    }

    private static IEnumerable<FileNode> EnumerateFiles(FileNode node)
    {
        if (node.Type == FileNodeType.File)
        {
            yield return node;
            yield break;
        }

        foreach (var child in node.Children ?? [])
        foreach (var file in EnumerateFiles(child))
            yield return file;
    }
}
