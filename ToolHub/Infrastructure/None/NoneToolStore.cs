using ToolHub.Application.Abstractions;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.None;

public sealed class NoneToolStore : IToolStore
{
    public Task<DataResult<IReadOnlyList<ToolEntry>>> ListAsync(CancellationToken ct)
        => Task.FromResult(new DataResult<IReadOnlyList<ToolEntry>>(
            DataStatus.Disabled,
            Array.Empty<ToolEntry>(),
            "Data source disabled (dev mode)."));

    public Task<DataResult<ToolEntry?>> GetAsync(string id, CancellationToken ct)
        => Task.FromResult(new DataResult<ToolEntry?>(
            DataStatus.Disabled,
            null,
            "Data source disabled (dev mode)."));
}
