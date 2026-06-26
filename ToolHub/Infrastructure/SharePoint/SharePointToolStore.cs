using System.Text.Json;
using Microsoft.Graph;
using ToolHub.Application.Abstractions;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class SharePointToolStore : IToolStore
{
    private readonly GraphServiceClient _graph;

    // 🔐 DRIVE ID – TEN SAM, KTÓRY DZIAŁAŁ W POWERSHELLU
    private const string DriveId =
        "b!2ge_DiOoQkCldtyXYKQlBt94szrqgR5FloI_q5-cMt2RpmoBW1JCTpwXmIwGZ5ND";

    // 📂 Ścieżka w RAMACH Drive (identyczna jak w PowerShellu)
    private const string ToolsPath =
        "Platform Components/Application Components/tools";

    public SharePointToolStore(GraphServiceClient graph)
    {
        _graph = graph;
    }

    public async Task<DataResult<IReadOnlyList<ToolEntry>>> ListAsync(CancellationToken ct)
    {
        try
        {
            Console.WriteLine(">>> SharePointToolStore.ListAsync ENTERED");

            // 1️⃣ Pobierz pliki z tools
            var items = await _graph
                .Drives[DriveId]
                .Root
                .ItemWithPath(ToolsPath)
                .Children
                .GetAsync(cancellationToken: ct);

            var tools = new List<ToolEntry>();

            if (items?.Value == null)
            {
                Console.WriteLine(">>> No items found in tools folder");
                return new DataResult<IReadOnlyList<ToolEntry>>(DataStatus.Ok, tools);
            }

            // 2️⃣ Czytaj tylko pliki *.json, ignoruj backupy restore_*.json
            foreach (var item in items.Value.Where(i => i.Name!.EndsWith(".json") && !i.Name!.StartsWith("restore_", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($">>> Reading JSON: {item.Name}");

                var stream = await _graph
                    .Drives[DriveId]
                    .Items[item.Id!]
                    .Content
                    .GetAsync(cancellationToken: ct);

                if (stream == null)
                    continue;

                var tool = await JsonSerializer.DeserializeAsync<ToolEntry>(
                    stream,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    },
                    ct);

                if (tool != null)
                    tools.Add(tool);
            }

            Console.WriteLine($">>> Tools loaded: {tools.Count}");

            return new DataResult<IReadOnlyList<ToolEntry>>(DataStatus.Ok, tools);
        }
        catch (Exception ex)
        {
            Console.WriteLine(">>> SharePointToolStore ERROR");
            Console.WriteLine(ex);

            return new DataResult<IReadOnlyList<ToolEntry>>(
                DataStatus.Error,
                Message: ex.Message);
        }
    }

    public Task<DataResult<ToolEntry?>> GetAsync(string id, CancellationToken ct)
        => throw new NotImplementedException();
}