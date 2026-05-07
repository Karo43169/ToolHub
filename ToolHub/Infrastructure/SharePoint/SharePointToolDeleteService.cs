using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolDeleteService
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string ToolsRoot =
        "Platform Components/Application Components/tools";

    public SharePointToolDeleteService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<ToolDeleteResult> DeleteAsync(
        ToolEntry tool,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var deletedToolJson = await TryDeleteByPathAsync(
            $"{ToolsRoot}/{tool.Id}.json",
            ct);

        var deletedToolFolder = false;

        if (!string.IsNullOrWhiteSpace(tool.ToolFolderPath))
        {
            deletedToolFolder = await TryDeleteByPathAsync(
                tool.ToolFolderPath,
                ct);
        }

        return new ToolDeleteResult(
            DeletedToolJson: deletedToolJson,
            DeletedToolFolder: deletedToolFolder);
    }

    private async Task<bool> TryDeleteByPathAsync(
        string path,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalizedPath = NormalizePath(path);

        try
        {
            var item = await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath(normalizedPath)
                .GetAsync(cancellationToken: ct);

            if (item?.Id is null)
                return false;

            await _graph
                .Drives[DriveId]
                .Items[item.Id]
                .DeleteAsync(cancellationToken: ct);

            return true;
        }
        catch (ODataError)
        {
            return false;
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

public sealed record ToolDeleteResult(
    bool DeletedToolJson,
    bool DeletedToolFolder);
