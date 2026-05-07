using System.Text;
using System.Text.Json;
using Microsoft.Graph;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolUpdateService
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string ToolsRoot =
        "Platform Components/Application Components/tools";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public SharePointToolUpdateService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<ToolEntry> UpdateAsync(
        ToolEntry currentTool,
        ToolUpdateInput input,
        string updatedByOid,
        string updatedByName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(currentTool);
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(currentTool.Id))
            throw new InvalidOperationException("Tool id cannot be empty.");

        var tags = input.Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var updatedTool = currentTool with
        {
            Name = input.Name.Trim(),
            Category = input.Category.Trim(),
            Owner = input.Owner.Trim(),
            Status = input.Status.Trim(),
            Version = input.Version.Trim(),
            Description = input.Description.Trim(),
            Tags = tags,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedByOid = updatedByOid,
            UpdatedByName = updatedByName
        };

        var json = JsonSerializer.Serialize(updatedTool, JsonOptions);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath($"{ToolsRoot}/{updatedTool.Id}.json")
            .Content
            .PutAsync(stream, cancellationToken: ct);

        return updatedTool;
    }
}

public sealed record ToolUpdateInput(
    string Name,
    string Category,
    string Owner,
    string Status,
    string Version,
    string Description,
    string Tags);
