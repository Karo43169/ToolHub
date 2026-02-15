using ToolHub.Application.Abstractions;
using ToolHub.Domain.Models;

namespace ToolHub.State;

public sealed class ToolHubState
{
    private readonly IToolStore _store;

    public ToolHubState(IToolStore store)
    {
        _store = store;
    }

    public IReadOnlyList<ToolEntry> Tools { get; private set; } = [];
    public string SearchTerm { get; set; } = string.Empty;
    public DataStatus DataStatus { get; private set; } = DataStatus.Ok;
    public ViewMode CurrentView { get; private set; } = ViewMode.Cards;

    public void SetView(ViewMode mode)
    {
        CurrentView = mode;
        NotifyChanged();
    }

    public async Task LoadAsync()
    {
        var result = await _store.ListAsync(CancellationToken.None);

        DataStatus = result.Status;
        if (result.Status == DataStatus.Disabled)
        {
            Tools = new List<ToolEntry>
    {
        new(
            Id: "tool-001",
            Name: "DXQ Installer",
            Category: "Deployment",
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
            Category: "Utilities",
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




        NotifyChanged();
    }

    public IReadOnlyList<ToolEntry> Filtered
    {
        get
        {
            IEnumerable<ToolEntry> q = Tools;

            if (!string.IsNullOrWhiteSpace(SelectedCategory))
                q = q.Where(t => string.Equals(t.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SelectedStatus))
                q = q.Where(t => string.Equals(t.Status, SelectedStatus, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchTerm))
                q = q.Where(t =>
                    t.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));

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
        NotifyChanged();
    }

}

