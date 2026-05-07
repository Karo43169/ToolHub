using Microsoft.Extensions.DependencyInjection;
using ToolHub.Application.Abstractions;
using ToolHub.Domain.Models;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class ToolCatalogCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private int _backgroundRefreshRunning = 0;

    private IReadOnlyList<ToolEntry> _tools = [];
    private bool _isLoaded;
    private DateTimeOffset? _lastLoadedAtUtc;
    private string? _lastError;

    public ToolCatalogCache(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public bool IsLoaded => _isLoaded;
    public DateTimeOffset? LastLoadedAtUtc => _lastLoadedAtUtc;
    public string? LastError => _lastError;

    public async Task<DataResult<IReadOnlyList<ToolEntry>>> GetToolsAsync(CancellationToken ct)
    {
        if (_isLoaded)
        {
            return new DataResult<IReadOnlyList<ToolEntry>>(
                DataStatus.Ok,
                _tools);
        }

        return await RefreshAsync(ct);
    }

    public async Task<DataResult<IReadOnlyList<ToolEntry>>> RefreshAsync(CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IToolStore>();

            var result = await store.ListAsync(ct);

            if (result.Status == DataStatus.Ok)
            {
                _tools = result.Value ?? [];
                _isLoaded = true;
                _lastLoadedAtUtc = DateTimeOffset.UtcNow;
                _lastError = null;

                return new DataResult<IReadOnlyList<ToolEntry>>(
                    DataStatus.Ok,
                    _tools);
            }

            _lastError = result.Message;

            if (_isLoaded)
            {
                return new DataResult<IReadOnlyList<ToolEntry>>(
                    DataStatus.Ok,
                    _tools);
            }

            return result;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;

            if (_isLoaded)
            {
                return new DataResult<IReadOnlyList<ToolEntry>>(
                    DataStatus.Ok,
                    _tools);
            }

            return new DataResult<IReadOnlyList<ToolEntry>>(
                DataStatus.Error,
                Message: ex.Message);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void RefreshInBackground()
    {
        if (Interlocked.Exchange(ref _backgroundRefreshRunning, 1) == 1)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshAsync(CancellationToken.None);
            }
            finally
            {
                Interlocked.Exchange(ref _backgroundRefreshRunning, 0);
            }
        });
    }

    public void Invalidate()
    {
        _isLoaded = false;
        _tools = [];
        _lastLoadedAtUtc = null;
        _lastError = null;
    }
}
