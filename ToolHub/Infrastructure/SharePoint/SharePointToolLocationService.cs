using Microsoft.Graph;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolLocationService
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    public SharePointToolLocationService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<string> GetFolderWebUrlAsync(
        string folderPath,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("Folder path cannot be empty.", nameof(folderPath));

        var normalizedPath = NormalizePath(folderPath);

        var item = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(normalizedPath)
            .GetAsync(cancellationToken: ct);

        if (item?.WebUrl is null)
            throw new InvalidOperationException($"SharePoint folder was not found or has no web URL: {normalizedPath}");

        return item.WebUrl;
    }

    public async Task<IReadOnlyList<string>> ListArchiveVersionsAsync(string toolFolderPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(toolFolderPath))
            return Array.Empty<string>();

        var archivePath = NormalizePath($"{toolFolderPath}/archive");

        try
        {
            var archive = await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath(archivePath)
                .Children
                .GetAsync(cancellationToken: ct);

            if (archive?.Value == null)
                return Array.Empty<string>();

            return archive.Value
                .Where(d => !string.IsNullOrWhiteSpace(d.Name))
                .Select(d => d.Name!)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string NormalizePath(string path)
    {
        return path
            .Replace('\\', '/')
            .Trim()
            .Trim('/');
    }
}
