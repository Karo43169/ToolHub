using System.Text.Json;
using Microsoft.Graph;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointAdminService
{
    private readonly GraphServiceClient _graph;

    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    private const string AdminsJsonPath =
        "Platform Components/Application Components/admins.json";

    public SharePointAdminService(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<bool> IsAdminAsync(string oid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(oid))
            return false;

        try
        {
            var stream = await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath(AdminsJsonPath)
                .Content
                .GetAsync(cancellationToken: ct);

            if (stream is null)
                return false;

            var file = await JsonSerializer.DeserializeAsync<AdminsFile>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                ct);

            return file?.AdminOids.Any(x =>
                string.Equals(x, oid, StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class AdminsFile
    {
        public List<string> AdminOids { get; init; } = new();
    }
}