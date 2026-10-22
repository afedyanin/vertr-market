using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Market.Gateways.Tinvest.BackgroundServices;

public abstract class StreamServiceBase : BackgroundService
{
    protected readonly IServiceProvider _serviceProvider;
    protected readonly ILoggerFactory _loggerFactory;
    protected readonly TinvestSettings _tinvestSettings;

    protected virtual string ServiceName => this.GetType().Name;

    protected abstract bool IsEnabled { get; init; }

    protected abstract ILogger Logger { get; init; }

    protected StreamServiceBase(
        IServiceProvider serviceProvider,
        IOptions<TinvestSettings> tinvestOptions,
        ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _tinvestSettings = tinvestOptions.Value;
        _loggerFactory = loggerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            Logger.LogWarning($"{ServiceName} is disabled.");
            return;
        }

        try
        {
            await StartConsumingLoop(stoppingToken);
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, $"Unhandled exception in {ServiceName} master loop: {ex.Message}");
        }
        finally
        {
            Logger.LogInformation($"Stopping token signaled. Initiating graceful shutdown for channels...");
            await OnComplete();
            Logger.LogInformation($"{ServiceName} execution completed at {DateTime.UtcNow:O}");
        }
    }

    protected abstract ValueTask OnComplete();
    protected abstract Task Subscribe(Action onDataReceived, CancellationToken stoppingToken = default);

    private async Task StartConsumingLoop(CancellationToken stoppingToken)
    {
        var baseDelay = TimeSpan.FromSeconds(2);
        var maxDelay = TimeSpan.FromMinutes(1); // Защита: не ждем дольше одной минуты
        var currentDelay = baseDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            Logger.LogInformation($"{ServiceName} consuming started at {DateTime.UtcNow:O}");

            try
            {
                await Subscribe(onDataReceived: () =>
                {
                    if (currentDelay != baseDelay)
                    {
                        Logger.LogInformation($"{ServiceName} connection stable. Resetting retry delay to {baseDelay.TotalSeconds}s.");
                        currentDelay = baseDelay; // Сбрасываем задержку на исходную
                    }
                }, stoppingToken);
            }
            catch (RpcException rpcEx)
            {
                if (rpcEx.StatusCode != StatusCode.DeadlineExceeded)
                {
                    Logger.LogError(rpcEx, $"{ServiceName} gRPC consuming exception. StatusCode={rpcEx.StatusCode}, Message={rpcEx.Message}");
                }

                await SafeDelay(currentDelay, stoppingToken);
                currentDelay = MultiplyDelay(currentDelay, baseDelay, maxDelay);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"{ServiceName} network stream exception. Message={ex.Message}");

                await SafeDelay(currentDelay, stoppingToken);
                currentDelay = MultiplyDelay(currentDelay, baseDelay, maxDelay);
            }
        }
    }

    private TimeSpan MultiplyDelay(TimeSpan current, TimeSpan baseValue, TimeSpan max)
    {
        // Экспоненциальное увеличение: 2с -> 4с -> 8с -> 16с -> 32с -> 60с
        var nextDelay = TimeSpan.FromSeconds(current.TotalSeconds * 2);
        return nextDelay > max ? max : nextDelay;
    }

    private async Task SafeDelay(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            Logger.LogWarning($"Waiting {delay.TotalSeconds} seconds before reconnecting...");
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Игнорируем, приложение завершает работу
        }
    }
}
