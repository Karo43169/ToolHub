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

    // Niedozwolone znaki i prosty sanitizer dla nazw SharePoint/OneDrive
    private static readonly char[] SharePointIllegalChars = new[] { '"', '*', ':', '<', '>', '?', '/', '\\', '|', '#', '%', '&', '{', '}', '~' };
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON","PRN","AUX","NUL",
        "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
        "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
    };

    private static string SanitizeSharePointName(string name, string fallback = "item")
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        var sb = new System.Text.StringBuilder();
        foreach (var ch in name)
        {
            sb.Append(Array.IndexOf(SharePointIllegalChars, ch) >= 0 ? '-' : ch);
        }
        var outName = sb.ToString().Trim();
        // Usuń końcowe spacje i kropki
        outName = outName.TrimEnd(' ', '.');
        if (string.IsNullOrWhiteSpace(outName) || ReservedNames.Contains(outName))
            return $"{fallback}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        if (outName.Length > 128)
            outName = outName.Substring(0, 128);
        return outName;
    }

    public SharePointToolRequestService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<ToolRequestResult> CreateAsync(
        CreateToolRequestInput input,
        CancellationToken ct)
    {
        var requestId = $"REQ-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}";

        var sanitizedFolderName = SanitizeSharePointName(requestId);

        var folder = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(ApplicationReqRoot)
            .Children
            .PostAsync(new DriveItem
            {
                Name = sanitizedFolderName,
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

            applicationReqFolder = $"{ApplicationReqRoot}/{sanitizedFolderName}",
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

    public async Task<ToolRequestResult> CreateUpdateAsync(
        CreateToolUpdateRequestInput input,
        CancellationToken ct)
    {
        var requestId = $"REQ-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}";

        var sanitizedFolderName = SanitizeSharePointName(requestId);

        var folder = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(ApplicationReqRoot)
            .Children
            .PostAsync(new DriveItem
            {
                Name = sanitizedFolderName,
                Folder = new Folder()
            }, cancellationToken: ct);

        var requestJson = new
        {
            requestId,
            type = "UpdateTool",
            status = "Pending",
            requestedAtUtc = DateTime.UtcNow,

            requestedByOid = input.RequestedByOid,
            requestedByName = input.RequestedByName,
            requestedByEmail = input.RequestedByEmail,

            applicationReqFolder = $"{ApplicationReqRoot}/{sanitizedFolderName}",
            applicationReqFolderUrl = folder?.WebUrl,

            targetToolId = input.TargetToolId,
            reason = input.Reason,
            requestedVersion = input.RequestedVersion,
            notes = input.Notes,

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

public sealed record CreateToolUpdateRequestInput(
    string TargetToolId,
    string Name,
    string Category,
    string Owner,
    string Status,
    string Version,
    string Description,
    string Tags,
    string RequestedVersion,
    string Reason,
    string Notes,
    string RequestedByOid,
    string RequestedByName,
    string RequestedByEmail
);

public sealed record ToolRequestResult(
    string RequestId,
    string FolderUrl
);
