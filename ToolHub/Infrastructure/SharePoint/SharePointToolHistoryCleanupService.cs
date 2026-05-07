using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolHistoryCleanupService
{
    private readonly GraphServiceClient _graph;
    private readonly SharePointToolRequestReader _requestReader;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string RequestJsonRoot =
        "Platform Components/Application Components/request";

    public SharePointToolHistoryCleanupService(
        GraphServiceClient graph,
        SharePointToolRequestReader requestReader)
    {
        _graph = graph;
        _requestReader = requestReader;
    }

    public async Task<HistoryCleanupResult> ClearHistoryAsync(CancellationToken ct)
    {
        var historyRequests = await _requestReader.ListHistoryAsync(ct);

        var deletedRequestJsonCount = 0;
        var deletedRejectedFolderCount = 0;
        var skippedCount = 0;
        var errors = new List<string>();

        foreach (var request in historyRequests)
        {
            try
            {
                if (IsRejected(request) && !string.IsNullOrWhiteSpace(request.RejectedFolderPath))
                {
                    var deletedFolder = await TryDeleteByPathAsync(
                        request.RejectedFolderPath,
                        ct);

                    if (deletedFolder)
                        deletedRejectedFolderCount++;
                }

                var requestJsonPath = $"{RequestJsonRoot}/{request.RequestId}.json";
                var deletedJson = await TryDeleteByPathAsync(requestJsonPath, ct);

                if (deletedJson)
                    deletedRequestJsonCount++;
                else
                    skippedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{request.RequestId}: {ex.Message}");
            }
        }

        return new HistoryCleanupResult(
            DeletedRequestJsonCount: deletedRequestJsonCount,
            DeletedRejectedFolderCount: deletedRejectedFolderCount,
            SkippedCount: skippedCount,
            Errors: errors);
    }

    private async Task<bool> TryDeleteByPathAsync(string path, CancellationToken ct)
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

    private static bool IsRejected(ToolRequestEntry request)
    {
        return string.Equals(request.Status, "Rejected", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path
            .Replace('\\', '/')
            .Trim()
            .Trim('/');
    }
}

public sealed record HistoryCleanupResult(
    int DeletedRequestJsonCount,
    int DeletedRejectedFolderCount,
    int SkippedCount,
    IReadOnlyList<string> Errors)
{
    public bool HasErrors => Errors.Count > 0;
}
