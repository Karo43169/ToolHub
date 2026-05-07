using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointRejectedArchiveService
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string ComponentsRoot =
        "Platform Components/Application Components";

    private const string RejectedArchiveRoot =
        "Platform Components/Application Components/rejected";

    private const string RejectedArchiveFolderName =
        "rejected";

    public SharePointRejectedArchiveService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<RejectedArchiveLocation> ArchiveAsync(
        ToolRequestEntry request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ApplicationReqFolder))
            throw new InvalidOperationException("Request does not contain ApplicationReqFolder path.");

        var sourceFolderPath = NormalizePath(request.ApplicationReqFolder);
        var rejectedFolderName = request.RequestId;
        var rejectedFolderPath = $"{RejectedArchiveRoot}/{rejectedFolderName}";

        var sourceFolder = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(sourceFolderPath)
            .GetAsync(cancellationToken: ct);

        if (sourceFolder?.Id is null)
            throw new InvalidOperationException($"Source request folder was not found: {sourceFolderPath}");

        var archiveRoot = await EnsureRejectedArchiveRootAsync(ct);

        if (archiveRoot?.Id is null)
            throw new InvalidOperationException($"Rejected archive root folder was not found or could not be created: {RejectedArchiveRoot}");

        var moveRequest = new DriveItem
        {
            Name = rejectedFolderName,
            ParentReference = new ItemReference
            {
                Id = archiveRoot.Id
            }
        };

        var rejectedFolder = await _graph
            .Drives[DriveId]
            .Items[sourceFolder.Id]
            .PatchAsync(moveRequest, cancellationToken: ct);

        return new RejectedArchiveLocation(
            FolderName: rejectedFolderName,
            FolderPath: rejectedFolderPath,
            FolderUrl: rejectedFolder?.WebUrl ?? string.Empty);
    }

    private async Task<DriveItem?> EnsureRejectedArchiveRootAsync(CancellationToken ct)
    {
        var existing = await TryGetItemByPathAsync(RejectedArchiveRoot, ct);

        if (existing is not null)
            return existing;

        var componentsRoot = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(ComponentsRoot)
            .GetAsync(cancellationToken: ct);

        if (componentsRoot?.Id is null)
            throw new InvalidOperationException($"Components root folder was not found: {ComponentsRoot}");

        return await _graph
            .Drives[DriveId]
            .Items[componentsRoot.Id]
            .Children
            .PostAsync(new DriveItem
            {
                Name = RejectedArchiveFolderName,
                Folder = new Folder()
            }, cancellationToken: ct);
    }

    private async Task<DriveItem?> TryGetItemByPathAsync(string path, CancellationToken ct)
    {
        try
        {
            return await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath(path)
                .GetAsync(cancellationToken: ct);
        }
        catch (ODataError)
        {
            return null;
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

public sealed record RejectedArchiveLocation(
    string FolderName,
    string FolderPath,
    string FolderUrl);
