using System.Threading.Channels;

namespace hrms_api.Services;

public sealed record ScreeningBatchWorkItem(string BatchId);

public interface IScreeningBatchQueue
{
    ValueTask EnqueueAsync(ScreeningBatchWorkItem item, CancellationToken cancellationToken = default);
    ValueTask<ScreeningBatchWorkItem> DequeueAsync(CancellationToken cancellationToken = default);
}

public class ScreeningBatchQueue : IScreeningBatchQueue
{
    private readonly Channel<ScreeningBatchWorkItem> _queue =
        Channel.CreateUnbounded<ScreeningBatchWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(ScreeningBatchWorkItem item, CancellationToken cancellationToken = default) =>
        _queue.Writer.WriteAsync(item, cancellationToken);

    public ValueTask<ScreeningBatchWorkItem> DequeueAsync(CancellationToken cancellationToken = default) =>
        _queue.Reader.ReadAsync(cancellationToken);
}

public class ScreeningBatchWorker : BackgroundService
{
    private readonly IScreeningBatchQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScreeningBatchWorker> _logger;

    public ScreeningBatchWorker(
        IScreeningBatchQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ScreeningBatchWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ScreeningBatchWorkItem item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var batchService = scope.ServiceProvider.GetRequiredService<IScreeningBatchService>();
                await batchService.ProcessBatchAsync(item.BatchId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while processing screening batch {BatchId}", item.BatchId);
            }
        }
    }
}
