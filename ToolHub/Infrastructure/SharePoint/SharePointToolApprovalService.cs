using System.Text;
using System.Text.Json;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolApprovalService
{
    private readonly GraphServiceClient _graph;
    private readonly SharePointToolPublishService _publishService;
    private readonly SharePointRejectedArchiveService _rejectedArchiveService;
    private readonly SharePointToolUpdateService _updateService;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string ToolsRoot =
        "Platform Components/Application Components/tools";

    private const string RequestJsonRoot =
        "Platform Components/Application Components/request";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SharePointToolApprovalService(
        GraphServiceClient graph,
        SharePointToolPublishService publishService,
        SharePointRejectedArchiveService rejectedArchiveService,
        SharePointToolUpdateService updateService)
    {
        _graph = graph;
        _publishService = publishService;
        _rejectedArchiveService = rejectedArchiveService;
        _updateService = updateService;
    }

    public async Task<ToolEntry> ApproveAsync(
        ToolRequestEntry request,
        string approvedByOid,
        string approvedByName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Handle update requests separately
        if (string.Equals(request.Type, "UpdateTool", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.TargetToolId))
                throw new InvalidOperationException("Update request missing TargetToolId.");

            // Load current tool JSON
            var toolJsonStream = await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath($"{ToolsRoot}/{request.TargetToolId}.json")
                .Content
                .GetAsync(cancellationToken: ct);

            if (toolJsonStream == null)
                throw new InvalidOperationException($"Tool JSON not found: {request.TargetToolId}");

            var currentTool = await JsonSerializer.DeserializeAsync<ToolEntry>(toolJsonStream, JsonOptions, ct);
            if (currentTool == null)
                throw new InvalidOperationException("Failed to deserialize existing tool JSON.");

            // Perform JSON update
            // Use requested version if provided
            var requestedVersion = string.IsNullOrWhiteSpace(request.RequestedVersion)
                ? request.Tool.Version
                : request.RequestedVersion.Trim();

            var updateInput = new ToolUpdateInput(
                Name: request.Tool.Name,
                Category: request.Tool.Category,
                Owner: request.Tool.Owner,
                Status: request.Tool.Status,
                Version: requestedVersion,
                Description: request.Tool.Description,
                Tags: request.Tool.Tags
            );

            var updatedTool = await _updateService.UpdateAsync(currentTool, updateInput, approvedByOid, approvedByName, ct);

            // Move existing published files into archive subfolder under the published folder
            var publishedFolder = await TryGetItemByPathAsync(updatedTool.ToolFolderPath, ct);
            if (publishedFolder?.Id is null)
                throw new InvalidOperationException($"Published folder was not found: {updatedTool.ToolFolderPath}");

            // Ensure 'archive' folder exists inside published folder
            var archivePath = $"{updatedTool.ToolFolderPath}/archive";
            var archiveFolder = await TryGetItemByPathAsync(archivePath, ct);
            if (archiveFolder?.Id is null)
            {
                archiveFolder = await _graph
                    .Drives[DriveId]
                    .Items[publishedFolder.Id]
                    .Children
                    .PostAsync(new DriveItem { Name = "archive", Folder = new Folder() }, cancellationToken: ct);
            }

            // Create version subfolder inside archive (e.g. v1.2.3)
            // Use previous (current) tool version to archive previous release
            var previousVersion = currentTool.Version ?? string.Empty;
            var versionFolderName = SharePointToolPublishService.ToSafeSharePointSegment($"v{previousVersion}");
            if (string.IsNullOrWhiteSpace(versionFolderName))
                versionFolderName = $"v{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Check if version subfolder exists
            DriveItem? versionFolder = null;
            try
            {
                versionFolder = await _graph
                    .Drives[DriveId]
                    .Root
                    .ItemWithPath($"{archivePath}/{versionFolderName}")
                    .GetAsync(cancellationToken: ct);
            }
            catch
            {
                // not found
            }

            if (versionFolder?.Id is null)
            {
                versionFolder = await _graph
                    .Drives[DriveId]
                    .Items[archiveFolder.Id]
                    .Children
                    .PostAsync(new DriveItem { Name = versionFolderName, Folder = new Folder() }, cancellationToken: ct);
            }

            // Move current children (files/folders) into archive/version folder (skip archive folder itself)
            var children = await _graph
                .Drives[DriveId]
                .Items[publishedFolder.Id]
                .Children
                .GetAsync(cancellationToken: ct);

            if (children?.Value != null)
            {
                foreach (var child in children.Value.Where(c => !string.Equals(c.Name, "archive", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        await _graph
                            .Drives[DriveId]
                            .Items[child.Id]
                            .PatchAsync(new DriveItem { ParentReference = new ItemReference { Id = versionFolder.Id } }, cancellationToken: ct);
                    }
                    catch
                    {
                        // Non-fatal: continue moving other items
                    }
                }
            }

            // Move incoming request contents into published folder
            if (string.IsNullOrWhiteSpace(request.ApplicationReqFolder))
                throw new InvalidOperationException("Request does not contain ApplicationReqFolder path.");

            var sourceFolder = await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath(NormalizePath(request.ApplicationReqFolder))
                .GetAsync(cancellationToken: ct);

            if (sourceFolder?.Id is null)
                throw new InvalidOperationException("Source request folder was not found.");

            var srcChildren = await _graph
                .Drives[DriveId]
                .Items[sourceFolder.Id]
                .Children
                .GetAsync(cancellationToken: ct);

            if (srcChildren?.Value != null)
            {
                foreach (var child in srcChildren.Value)
                {
                    try
                    {
                        await _graph
                            .Drives[DriveId]
                            .Items[child.Id]
                            .PatchAsync(new DriveItem { ParentReference = new ItemReference { Id = publishedFolder.Id } }, cancellationToken: ct);
                    }
                    catch
                    {
                        // Non-fatal
                    }
                }
            }

            return updatedTool;
        }

        // Default: create new tool
        var toolId = Guid.NewGuid().ToString("N");

        var publishedLocation = await _publishService.PublishAsync(
            request,
            toolId,
            ct);

        var tags = request.Tool.Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var tool = new ToolEntry(
            Id: toolId,
            Name: request.Tool.Name,
            Category: request.Tool.Category,
            Owner: request.Tool.Owner,
            Status: string.IsNullOrWhiteSpace(request.Tool.Status) ? "Active" : request.Tool.Status,
            Version: request.Tool.Version,
            Description: request.Tool.Description,
            Tags: tags,
            ToolFolderPath: publishedLocation.FolderPath,
            ManualPath: null,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedByOid: approvedByOid,
            UpdatedByName: approvedByName
        );

        var json = JsonSerializer.Serialize(
            tool,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath($"{ToolsRoot}/{toolId}.json")
            .Content
            .PutAsync(stream, cancellationToken: ct);

        return tool;
    }

    public async Task MarkApprovedAsync(
        ToolRequestEntry request,
        ToolEntry approvedTool,
        string approvedByOid,
        string approvedByName,
        CancellationToken ct)
    {
        var approvedRequest = request with
        {
            Status = "Approved",
            ApprovedAtUtc = DateTimeOffset.UtcNow,
            ApprovedByOid = approvedByOid,
            ApprovedByName = approvedByName,
            ApprovedToolId = approvedTool.Id
        };

        await WriteRequestJsonAsync(approvedRequest, ct);
    }

    public async Task MarkRejectedAsync(
        ToolRequestEntry request,
        string rejectedByOid,
        string rejectedByName,
        CancellationToken ct)
    {
        var rejectedArchiveLocation = await _rejectedArchiveService.ArchiveAsync(
            request,
            ct);

        var rejectedRequest = request with
        {
            Status = "Rejected",
            RejectedAtUtc = DateTimeOffset.UtcNow,
            RejectedByOid = rejectedByOid,
            RejectedByName = rejectedByName,
            RejectedFolderPath = rejectedArchiveLocation.FolderPath,
            RejectedFolderUrl = rejectedArchiveLocation.FolderUrl
        };

        await WriteRequestJsonAsync(rejectedRequest, ct);
    }

    private async Task WriteRequestJsonAsync(
        ToolRequestEntry request,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(
            request,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath($"{RequestJsonRoot}/{request.RequestId}.json")
            .Content
            .PutAsync(stream, cancellationToken: ct);
    }

    private static string NormalizePath(string path)
    {
        return path
            .Replace('\\', '/')
            .Trim()
            .Trim('/');
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
        catch
        {
            return null;
        }
    }
}
