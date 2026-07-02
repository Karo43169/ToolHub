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

    // Optional fields for update requests
    string? TargetToolId = null,
    string? Reason = null,
    string? RequestedVersion = null,
    string? Notes = null,

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
    string Tags,
    bool RestrictUpdateRequestsToOwner = false,
    string? AllowedUpdateRequesterEmails = null
);
