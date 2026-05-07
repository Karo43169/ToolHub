using Microsoft.Extensions.Hosting;

namespace ToolHub.Infrastructure.SharePoint;

public sealed class ToolCatalogWarmupService : BackgroundService
{
    private readonly ToolCatalogCache _cache;
    private readonly ILogger<ToolCatalogWarmupService> _logger;

    public ToolCatalogWarmupService(
        ToolCatalogCache cache,
        ILogger<ToolCatalogWarmupService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Tool catalog warmup started.");

            var result = await _cache.RefreshAsync(stoppingToken);

            _logger.LogInformation(
                "Tool catalog warmup finished with status {Status}. Loaded tools: {Count}.",
                result.Status,
                result.Value?.Count ?? 0);
        }
        catch (OperationCanceledException)
        {
            // App is stopping. Nothing to do.
        }
        catch (Exception ex)
        {
            // Do not block application startup if SharePoint is temporarily unavailable.
            _logger.LogWarning(ex, "Tool catalog warmup failed. The catalog will be loaded on first request.");
        }
    }
}
