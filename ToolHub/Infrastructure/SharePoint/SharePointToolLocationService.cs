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

    private static string NormalizePath(string path)
    {
        return path
            .Replace('\\', '/')
            .Trim()
            .Trim('/');
    }
}
