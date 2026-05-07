using System.Text;
using System.Text.Json;
using Microsoft.Graph;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolApprovalService
{
    private readonly GraphServiceClient _graph;
    private readonly SharePointToolPublishService _publishService;
    private readonly SharePointRejectedArchiveService _rejectedArchiveService;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string ToolsRoot =
        "Platform Components/Application Components/tools";

    private const string RequestJsonRoot =
        "Platform Components/Application Components/request";

    public SharePointToolApprovalService(
        GraphServiceClient graph,
        SharePointToolPublishService publishService,
        SharePointRejectedArchiveService rejectedArchiveService)
    {
        _graph = graph;
        _publishService = publishService;
        _rejectedArchiveService = rejectedArchiveService;
    }

    public async Task<ToolEntry> ApproveAsync(
        ToolRequestEntry request,
        string approvedByOid,
        string approvedByName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

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
}
