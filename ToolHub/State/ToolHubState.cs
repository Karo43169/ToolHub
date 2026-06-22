using ToolHub.Application.Abstractions;
using ToolHub.Domain.Models;
using ToolHub.Infrastructure.SharePoint;

namespace ToolHub.State;

public sealed class ToolHubState
{
    private readonly ToolCatalogCache _toolCatalogCache;
    private readonly SharePointToolFavoriteService _favoriteService;

    public ToolHubState(
        ToolCatalogCache toolCatalogCache,
        SharePointToolFavoriteService favoriteService)
    {
        _toolCatalogCache = toolCatalogCache;
        _favoriteService = favoriteService;
    }

    public IReadOnlyList<ToolEntry> Tools { get; private set; } = [];
    public string SearchTerm { get; set; } = string.Empty;
    public DataStatus DataStatus { get; private set; } = DataStatus.Ok;
    public ViewMode CurrentView { get; private set; } = ViewMode.Cards;

    public bool FavoritesOnly { get; private set; }
    public IReadOnlySet<string> FavoriteToolIds { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public void SetView(ViewMode mode)
    {
        CurrentView = mode;
        NotifyChanged();
    }

    public async Task LoadAsync()
    {
        var result = await _toolCatalogCache.GetToolsAsync(CancellationToken.None);

        DataStatus = result.Status;

        if (result.Status == DataStatus.Disabled)
        {
            Tools = new List<ToolEntry>
            {
                new(
                    Id: "tool-001",
                    Name: "DXQ Installer",
                    Category: "DXQ",
                    Owner: "Automation Team",
                    Status: "Active",
                    Version: "1.0.0",
                    Description: "Automatyczna instalacja obrazu DXQ na IPC (tryb dev demo).",
                    Tags: new List<string> { "dxq", "install", "ipc" },
                    ToolFolderPath: "tools/tool-001",
                    ManualPath: "manuals/dxq-installer.pdf",
                    UpdatedAtUtc: DateTimeOffset.UtcNow.AddDays(-2),
                    UpdatedByOid: "dev",
                    UpdatedByName: "Dev User"
                ),
                new(
                    Id: "tool-002",
                    Name: "DurrToolHub Updater",
                    Category: "Scripts / Excel Tools",
                    Owner: "IT Tools",
                    Status: "Preview",
                    Version: "0.9.3",
                    Description: "Aktualizator narzędzi i paczek. Wersja testowa.",
                    Tags: new List<string> { "update", "tools" },
                    ToolFolderPath: "tools/tool-002",
                    ManualPath: null,
                    UpdatedAtUtc: DateTimeOffset.UtcNow.AddDays(-7),
                    UpdatedByOid: "dev",
                    UpdatedByName: "Dev User"
                )
            };
        }
        else
        {
            Tools = result.Value ?? [];
        }

        if (Selected == null && Tools.Count > 0)
            Selected = Tools[0];

        if (Selected != null && Tools.All(t => t.Id != Selected.Id))
            Selected = Tools.FirstOrDefault();

        NotifyChanged();
    }

    public async Task RefreshFromSourceAsync()
    {
        await _toolCatalogCache.RefreshAsync(CancellationToken.None);
        await LoadAsync();
    }

    public async Task LoadFavoritesAsync(string userOid)
    {
        FavoriteToolIds = await _favoriteService.GetFavoriteToolIdsAsync(
            userOid,
            CancellationToken.None);

        NotifyChanged();
    }

    public async Task ToggleFavoriteAsync(string userOid, string toolId)
    {
        if (string.IsNullOrWhiteSpace(userOid))
            throw new InvalidOperationException("User OID cannot be empty.");

        if (string.IsNullOrWhiteSpace(toolId))
            throw new InvalidOperationException("Tool id cannot be empty.");

        // Zapisujemy poprzedni stan (rollback)
        var previousFavorites = new HashSet<string>(
            FavoriteToolIds,
            StringComparer.OrdinalIgnoreCase);

        // Liczymy nowy stan lokalnie
        var nextFavorites = new HashSet<string>(
            FavoriteToolIds,
            StringComparer.OrdinalIgnoreCase);

        if (!nextFavorites.Add(toolId))
            nextFavorites.Remove(toolId);

        // ✅ OPTIMISTIC UI – gwiazdka zapala się OD RAZU
        FavoriteToolIds = nextFavorites;
        NotifyChanged();

        // Pozwalamy UI się odmalować
        await Task.Yield();

        try
        {
            // Zapis do SharePoint w tle
            await _favoriteService.SaveFavoriteToolIdsAsync(
                userOid,
                nextFavorites,
                CancellationToken.None);
        }
        catch
        {
            // ❌ rollback jeśli zapis się nie uda
            FavoriteToolIds = previousFavorites;
            NotifyChanged();
            throw;
        }
    }


    public bool IsFavorite(string toolId)
    {
        return FavoriteToolIds.Contains(toolId);
    }

    public void SetFavoritesOnly(bool enabled)
    {
        FavoritesOnly = enabled;
        NotifyChanged();
    }

    public void ToggleFavoritesOnly()
    {
        FavoritesOnly = !FavoritesOnly;
        NotifyChanged();
    }

    public IReadOnlyList<ToolEntry> Filtered
    {
        get
        {
            IEnumerable<ToolEntry> q = Tools;

            if (FavoritesOnly)
                q = q.Where(t => FavoriteToolIds.Contains(t.Id));

            if (!string.IsNullOrWhiteSpace(SelectedCategory))
                q = q.Where(t => string.Equals(t.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SelectedStatus))
                q = q.Where(t => string.Equals(t.Status, SelectedStatus, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                q = q.Where(t =>
                    t.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    t.Tags.Any(tag => tag.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    t.Owner.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    t.Category.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            q = CurrentSort switch
            {
                SortMode.LastUpdatedDesc => q.OrderByDescending(t => t.UpdatedAtUtc),
                SortMode.NameAsc => q.OrderBy(t => t.Name),
                SortMode.CategoryAsc => q.OrderBy(t => t.Category).ThenBy(t => t.Name),
                _ => q
            };

            return q.ToList();
        }
    }

    public ToolEntry? Selected { get; private set; }

    public void Select(string id)
    {
        Selected = Tools.FirstOrDefault(t => t.Id == id);
        NotifyChanged();
    }

    public event Action? Changed;
    private void NotifyChanged() => Changed?.Invoke();

    public void Add(ToolEntry tool)
    {
        var list = Tools.ToList();
        list.Add(tool);
        Tools = list;
        Selected = tool;
        NotifyChanged();
    }

    public void Update(ToolEntry tool)
    {
        var list = Tools.ToList();
        var index = list.FindIndex(t => t.Id == tool.Id);

        if (index >= 0)
        {
            list[index] = tool;
            Tools = list;
            Selected = tool;
            NotifyChanged();
        }
    }

    public void Delete(string id)
    {
        var list = Tools.ToList();
        list.RemoveAll(t => t.Id == id);
        Tools = list;

        if (Selected?.Id == id)
            Selected = Tools.FirstOrDefault();

        NotifyChanged();
    }

    public string? SelectedCategory { get; private set; }
    public string? SelectedStatus { get; private set; }

    public void SetCategory(string? category)
    {
        SelectedCategory = category;
        NotifyChanged();
    }

    public void SetStatus(string? status)
    {
        SelectedStatus = status;
        NotifyChanged();
    }

    public void ClearFilters()
    {
        SelectedCategory = null;
        SelectedStatus = null;
        FavoritesOnly = false;
        NotifyChanged();
    }

    public IReadOnlyList<string> AvailableCategories =>
        Tools.Select(t => t.Category)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

    public IReadOnlyList<string> AvailableStatuses =>
        Tools.Select(t => t.Status)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

    public SortMode CurrentSort { get; private set; } = SortMode.LastUpdatedDesc;

    public void SetSort(SortMode mode)
    {
        CurrentSort = mode;
        FavoritesOnly = false;
        NotifyChanged();
    }
}
