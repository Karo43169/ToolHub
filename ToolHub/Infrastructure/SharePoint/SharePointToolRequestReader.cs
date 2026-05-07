using System.Text.Json;
using Microsoft.Graph;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolRequestReader
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string RequestJsonRoot =
        "Platform Components/Application Components/request";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SharePointToolRequestReader(GraphServiceClient graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Backward-compatible method used by the current admin panel.
    /// It returns only pending requests.
    /// </summary>
    public Task<IReadOnlyList<ToolRequestEntry>> ListAsync(CancellationToken ct)
    {
        return ListPendingAsync(ct);
    }

    /// <summary>
    /// Returns requests waiting for admin action.
    /// </summary>
    public async Task<IReadOnlyList<ToolRequestEntry>> ListPendingAsync(CancellationToken ct)
    {
        var requests = await ListAllAsync(ct);

        return requests
            .Where(x => string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.RequestedAtUtc)
            .ToList();
    }

    /// <summary>
    /// Returns processed requests: Approved and Rejected.
    /// </summary>
    public async Task<IReadOnlyList<ToolRequestEntry>> ListHistoryAsync(CancellationToken ct)
    {
        var requests = await ListAllAsync(ct);

        return requests
            .Where(x =>
                string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(GetProcessedAtUtc)
            .ThenByDescending(x => x.RequestedAtUtc)
            .ToList();
    }

    /// <summary>
    /// Returns every request JSON from SharePoint without status filtering.
    /// </summary>
    public async Task<IReadOnlyList<ToolRequestEntry>> ListAllAsync(CancellationToken ct)
    {
        var items = await _graph
            .Drives[DriveId]
            .Root
            .ItemWithPath(RequestJsonRoot)
            .Children
            .GetAsync(cancellationToken: ct);

        var requests = new List<ToolRequestEntry>();

        if (items?.Value == null)
            return requests;

        foreach (var item in items.Value.Where(x =>
                     x.Name != null &&
                     x.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            var stream = await _graph
                .Drives[DriveId]
                .Items[item.Id!]
                .Content
                .GetAsync(cancellationToken: ct);

            if (stream == null)
                continue;

            var request = await JsonSerializer.DeserializeAsync<ToolRequestEntry>(
                stream,
                JsonOptions,
                ct);

            if (request != null)
                requests.Add(request);
        }

        return requests
            .OrderByDescending(x => x.RequestedAtUtc)
            .ToList();
    }

    private static DateTimeOffset GetProcessedAtUtc(ToolRequestEntry request)
    {
        if (string.Equals(request.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            return request.ApprovedAtUtc ?? request.RequestedAtUtc;

        if (string.Equals(request.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            return request.RejectedAtUtc ?? request.RequestedAtUtc;

        return request.RequestedAtUtc;
    }
}
