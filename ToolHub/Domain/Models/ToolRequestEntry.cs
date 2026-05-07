namespace ToolHub.Domain.Models;

public sealed record ToolRequestEntry(
    string RequestId,
    string Type,
    string Status,
    DateTimeOffset RequestedAtUtc,
    string RequestedByOid,
    string RequestedByName,
    string RequestedByEmail,
    string ApplicationReqFolder,
    string ApplicationReqFolderUrl,
    ToolRequestMetadata Tool,

    DateTimeOffset? ApprovedAtUtc = null,
    string? ApprovedByOid = null,
    string? ApprovedByName = null,
    string? ApprovedToolId = null,

    DateTimeOffset? RejectedAtUtc = null,
    string? RejectedByOid = null,
    string? RejectedByName = null,
    string? RejectedFolderPath = null,
    string? RejectedFolderUrl = null
);

public sealed record ToolRequestMetadata(
    string Name,
    string Category,
    string Owner,
    string Status,
    string Version,
    string Description,
    string Tags
);
