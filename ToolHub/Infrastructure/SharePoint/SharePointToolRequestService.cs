using System.Text;
using System.Text.Json;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolRequestService
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string ApplicationReqRoot =
        "Platform Components/Application Req";

    private const string RequestJsonRoot =
        "Platform Components/Application Components/request";

    public SharePointToolRequestService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<ToolRequestResult> CreateAsync(
        CreateToolRequestInput input,
        CancellationToken ct)
    {
        var requestId = $"REQ-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}";

        var folder = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(ApplicationReqRoot)
            .Children
            .PostAsync(new DriveItem
            {
                Name = requestId,
                Folder = new Folder()
            }, cancellationToken: ct);

        var requestJson = new
        {
            requestId,
            type = "AddTool",
            status = "Pending",
            requestedAtUtc = DateTime.UtcNow,

            requestedByOid = input.RequestedByOid,
            requestedByName = input.RequestedByName,
            requestedByEmail = input.RequestedByEmail,

            applicationReqFolder = $"{ApplicationReqRoot}/{requestId}",
            applicationReqFolderUrl = folder?.WebUrl,

            tool = new
            {
                name = input.Name,
                category = input.Category,
                owner = input.Owner,
                status = input.Status,
                version = input.Version,
                description = input.Description,
                tags = input.Tags
            }
        };

        var json = JsonSerializer.Serialize(
            requestJson,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath($"{RequestJsonRoot}/{requestId}.json")
            .Content
            .PutAsync(stream, cancellationToken: ct);

        return new ToolRequestResult(
            RequestId: requestId,
            FolderUrl: folder?.WebUrl ?? "");
    }
}

public sealed record CreateToolRequestInput(
    string Name,
    string Category,
    string Owner,
    string Status,
    string Version,
    string Description,
    string Tags,
    string RequestedByOid,
    string RequestedByName,
    string RequestedByEmail
);

public sealed record ToolRequestResult(
    string RequestId,
    string FolderUrl
);