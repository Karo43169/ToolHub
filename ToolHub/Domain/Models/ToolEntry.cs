namespace ToolHub.Domain.Models;

public sealed record ToolEntry(
    string Id,
    string Name,
    string Category,
    string Owner,
    string Status,
    string Version,
    string Description,
    IReadOnlyList<string> Tags,
    string ToolFolderPath,
    string? ManualPath,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedByOid,
    string UpdatedByName
);
