using ToolHub.Domain.Models;

namespace ToolHub.Application.Abstractions;

public enum DataStatus
{
    Ok,
    Disabled,
    Forbidden,
    NotFound,
    Conflict,
    Transient,
    Error
}

public sealed record DataResult<T>(DataStatus Status, T? Value = default, string? Message = null);

public interface IToolStore
{
    Task<DataResult<IReadOnlyList<ToolEntry>>> ListAsync(CancellationToken ct);
    Task<DataResult<ToolEntry?>> GetAsync(string id, CancellationToken ct);
}
