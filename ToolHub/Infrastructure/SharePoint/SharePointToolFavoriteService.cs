using System.Text;
using System.Text.Json;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolFavoriteService
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string ComponentsRoot =
        "Platform Components/Application Components";

    private const string FavoritesRoot =
        "Platform Components/Application Components/favorites";

    private const string FavoritesFolderName =
        "favorites";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public SharePointToolFavoriteService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<IReadOnlySet<string>> GetFavoriteToolIdsAsync(
        string userOid,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userOid))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var path = GetFavoriteFilePath(userOid);

        try
        {
            var stream = await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath(path)
                .Content
                .GetAsync(cancellationToken: ct);

            if (stream is null)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var favoriteList = await JsonSerializer.DeserializeAsync<ToolFavoriteList>(
                stream,
                ReadOptions,
                ct);

            return new HashSet<string>(
                favoriteList?.ToolIds ?? [],
                StringComparer.OrdinalIgnoreCase);
        }
        catch (ODataError)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<IReadOnlySet<string>> ToggleFavoriteAsync(
        string userOid,
        string toolId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userOid))
            throw new InvalidOperationException("User OID cannot be empty.");

        if (string.IsNullOrWhiteSpace(toolId))
            throw new InvalidOperationException("Tool id cannot be empty.");

        var favoriteIds = new HashSet<string>(
            await GetFavoriteToolIdsAsync(userOid, ct),
            StringComparer.OrdinalIgnoreCase);

        if (!favoriteIds.Add(toolId))
            favoriteIds.Remove(toolId);

        await SaveFavoriteToolIdsAsync(userOid, favoriteIds, ct);

        return favoriteIds;
    }

    public async Task SaveFavoriteToolIdsAsync(
        string userOid,
        IReadOnlySet<string> toolIds,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userOid))
            throw new InvalidOperationException("User OID cannot be empty.");

        await EnsureFavoritesRootAsync(ct);

        var favoriteList = new ToolFavoriteList(
            UserOid: userOid,
            ToolIds: toolIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(favoriteList, WriteOptions);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(GetFavoriteFilePath(userOid))
            .Content
            .PutAsync(stream, cancellationToken: ct);
    }

    private async Task EnsureFavoritesRootAsync(CancellationToken ct)
    {
        var existing = await TryGetItemByPathAsync(FavoritesRoot, ct);

        if (existing is not null)
            return;

        var componentsRoot = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(ComponentsRoot)
            .GetAsync(cancellationToken: ct);

        if (componentsRoot?.Id is null)
            throw new InvalidOperationException($"Components root folder was not found: {ComponentsRoot}");

        await _graph
            .Drives[DriveId]
            .Items[componentsRoot.Id]
            .Children
            .PostAsync(new DriveItem
            {
                Name = FavoritesFolderName,
                Folder = new Folder()
            }, cancellationToken: ct);
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
        catch (ODataError)
        {
            return null;
        }
    }

    private static string GetFavoriteFilePath(string userOid)
    {
        return $"{FavoritesRoot}/{SafeFileName(userOid)}.json";
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();

        return new string(chars);
    }
}

public sealed record ToolFavoriteList(
    string UserOid,
    IReadOnlyList<string> ToolIds,
    DateTimeOffset UpdatedAtUtc);
