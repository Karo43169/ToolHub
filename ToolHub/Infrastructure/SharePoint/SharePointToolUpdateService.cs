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

        // Create a restore point by saving the current tool JSON before applying updates.
        try
        {
            // Remove any previous restore files for this tool to avoid cluttering the folder.
            try
            {
                var children = await _graph.Drives[DriveId].Root.ItemWithPath(ToolsRoot).Children.GetAsync(cancellationToken: ct);
                if (children?.Value != null)
                {
                    foreach (var child in children.Value.Where(c => !string.IsNullOrEmpty(c.Name) && c.Name.StartsWith($"restore_{currentTool.Id}_", StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(child.Id))
                                await _graph.Drives[DriveId].Items[child.Id].DeleteAsync(cancellationToken: ct);
                        }
                        catch
                        {
                            // ignore deletion errors for individual restore files
                        }
                    }
                }
            }
            catch
            {
                // ignore listing/deletion errors - continue to create a new restore
            }

            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var restorePath = $"{ToolsRoot}/restore_{currentTool.Id}_{timestamp}.json";
            var currentJson = JsonSerializer.Serialize(currentTool, JsonOptions);
            using var rs = new MemoryStream(Encoding.UTF8.GetBytes(currentJson));
            await _graph.Drives[DriveId].Root.ItemWithPath(restorePath).Content.PutAsync(rs, cancellationToken: ct);
        }
        catch
        {
            // ignore restore point errors - update should still proceed
        }

        var tags = input.Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var allowedList = input.AllowedUpdateRequesterEmails is null
            ? new List<string>()
            : input.AllowedUpdateRequesterEmails
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
            RestrictUpdateRequestsToOwner = input.RestrictUpdateRequestsToOwner,
            AllowedUpdateRequesterEmails = allowedList,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedByOid = updatedByOid,
            UpdatedByName = updatedByName,
            ChangeLog = input.ChangeLog ?? currentTool.ChangeLog
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
    string Tags,
    bool RestrictUpdateRequestsToOwner = false,
    string? AllowedUpdateRequesterEmails = null,
    IReadOnlyList<ChangeLogEntry>? ChangeLog = null);
