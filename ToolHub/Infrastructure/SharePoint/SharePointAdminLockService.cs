using System.Text;
using System.Text.Json;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointAdminLockService
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string ComponentsRoot =
        "Platform Components/Application Components";

    private const string LockFileName =
        "admin-lock.json";

    private const int LockTimeoutMinutes = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SharePointAdminLockService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<AdminLockResult> TryAcquireAsync(
        string adminOid,
        string adminName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(adminOid))
            return AdminLockResult.Denied("Missing admin OID.");

        var lockPath = $"{ComponentsRoot}/{LockFileName}";
        var existingLock = await TryReadLockAsync(lockPath, ct);

        if (existingLock is not null)
        {
            if (IsCurrentAdminLock(existingLock, adminOid))
            {
                await WriteLockAsync(lockPath, CreateLock(adminOid, adminName), ct);
                return AdminLockResult.Acquired(existingLock.LockedByName, existingLock.LockedAtUtc);
            }

            if (!IsExpired(existingLock))
            {
                return AdminLockResult.Denied(
                    $"Administration panel is locked by {existingLock.LockedByName} since {existingLock.LockedAtUtc.LocalDateTime}.",
                    existingLock.LockedByOid,
                    existingLock.LockedByName,
                    existingLock.LockedAtUtc);
            }
        }

        var newLock = CreateLock(adminOid, adminName);
        await WriteLockAsync(lockPath, newLock, ct);

        return AdminLockResult.Acquired(newLock.LockedByName, newLock.LockedAtUtc);
    }

    public async Task ReleaseAsync(string adminOid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(adminOid))
            return;

        var lockPath = $"{ComponentsRoot}/{LockFileName}";
        var existingLock = await TryReadLockAsync(lockPath, ct);

        if (existingLock is null)
            return;

        if (!IsCurrentAdminLock(existingLock, adminOid))
            return;

        await DeleteLockAsync(lockPath, ct);
    }

    private async Task<AdminLockFile?> TryReadLockAsync(string path, CancellationToken ct)
    {
        try
        {
            var stream = await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath(path)
                .Content
                .GetAsync(cancellationToken: ct);

            if (stream is null)
                return null;

            return await JsonSerializer.DeserializeAsync<AdminLockFile>(
                stream,
                JsonOptions,
                ct);
        }
        catch (ODataError)
        {
            return null;
        }
    }

    private async Task WriteLockAsync(string path, AdminLockFile lockFile, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(lockFile, JsonOptions);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(path)
            .Content
            .PutAsync(stream, cancellationToken: ct);
    }

    private async Task DeleteLockAsync(string path, CancellationToken ct)
    {
        try
        {
            var item = await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath(path)
                .GetAsync(cancellationToken: ct);

            if (item?.Id is null)
                return;

            await _graph
                .Drives[DriveId]
                .Items[item.Id]
                .DeleteAsync(cancellationToken: ct);
        }
        catch
        {
            // ignore
        }
    }

    private static AdminLockFile CreateLock(string adminOid, string adminName)
    {
        return new AdminLockFile(
            LockedByOid: adminOid,
            LockedByName: string.IsNullOrWhiteSpace(adminName) ? "Unknown admin" : adminName,
            LockedAtUtc: DateTimeOffset.UtcNow,
            ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(LockTimeoutMinutes));
    }

    private static bool IsCurrentAdminLock(AdminLockFile lockFile, string adminOid)
        => string.Equals(lockFile.LockedByOid, adminOid, StringComparison.OrdinalIgnoreCase);

    private static bool IsExpired(AdminLockFile lockFile)
        => lockFile.ExpiresAtUtc <= DateTimeOffset.UtcNow;

    private sealed record AdminLockFile(
        string LockedByOid,
        string LockedByName,
        DateTimeOffset LockedAtUtc,
        DateTimeOffset ExpiresAtUtc);
}

public sealed record AdminLockResult(
    bool IsAcquired,
    string? Message,
    string? LockedByOid,
    string? LockedByName,
    DateTimeOffset? LockedAtUtc)
{
    public static AdminLockResult Acquired(
        string? lockedByName = null,
        DateTimeOffset? lockedAtUtc = null)
        => new(true, null, null, lockedByName, lockedAtUtc);

    public static AdminLockResult Denied(
        string message,
        string? lockedByOid = null,
        string? lockedByName = null,
        DateTimeOffset? lockedAtUtc = null)
        => new(false, message, lockedByOid, lockedByName, lockedAtUtc);
}
