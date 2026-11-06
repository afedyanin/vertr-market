using System.Collections.Concurrent;
using System.Threading.Channels;
using Market.Core.Algos;
using Market.Core.Models;
using Market.Core.Repositories;
using Market.Infrastructure;
namespace Market.Host.BackgroundServices;

public class PairTradingStrategyService : BackgroundService, IAsyncDisposable
{
    private sealed class TaskInfo
    {
        public required CancellationTokenSource TokenSource { get; init; }
        public required Task ExecutingTask { get; init; }
    }

    private readonly IAlgoMetadataRepository _algoMetadataRepository;
    private readonly OrderBooksTopic _orderBooksTopic;
    private readonly ChannelWriter<IPairTradingSignal> _signalWriter;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PairTradingStrategyService> _logger;
    private readonly ConcurrentDictionary<Guid, TaskInfo> _strategyTasks = [];
    private CancellationToken _stoppingToken = default;
    private bool _disposed;


    public PairTradingStrategyService(
        IAlgoMetadataRepository algoMetadataRepository,
        Channel<OrderBookDepth> orderBookChannel,
        Channel<IPairTradingSignal> signalsChannel,
        ILoggerFactory loggerFactory)
    {
        _algoMetadataRepository = algoMetadataRepository;
        _algoMetadataRepository.OnSaved += OnAlgoSaved;
        _algoMetadataRepository.OnDeleted += OnAlgoDeleted;

        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<PairTradingStrategyService>();

        _orderBooksTopic = new OrderBooksTopic(orderBookChannel, loggerFactory);
        _signalWriter = signalsChannel.Writer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting PairTradingStrategyService...");

        _stoppingToken = stoppingToken;

        var orderBooksTopicTask = _orderBooksTopic.ExecuteAsync(stoppingToken);
        var strategiesTask = StartStrategies(stoppingToken);
        await Task.WhenAll(orderBooksTopicTask, strategiesTask);

        _logger.LogInformation("PairTradingStrategyService stopped.");
    }

    private Task StartStrategies(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var algoMeta in _algoMetadataRepository.GetAll())
            {
                try
                {
                    StartStrategyTask(algoMeta);
                    _logger.LogInformation("Strategy started: Id={StrategyId} AlgoType={AlgoType}", algoMeta.StrategyId, algoMeta.AlgoType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Strategy initializing error: Id={StrategyId} AlgoType={AlgoType} Message={Message}",
                        algoMeta.StrategyId,
                        algoMeta.AlgoType,
                        ex.Message);
                }
            }

            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PairTradingStrategyService failed to execute. Message={Message}", ex.Message);
        }

        return Task.CompletedTask;
    }

    public void OnAlgoSaved(AlgoMetadata algoMeta)
    {
        if (_disposed)
        {
            return;
        }

        StartStrategyTask(algoMeta);
        _logger.LogInformation("Strategy started: Id={StrategyId} AlgoType={AlgoType}", algoMeta.StrategyId, algoMeta.AlgoType);
    }
    public void OnAlgoDeleted(AlgoMetadata algoMetadata)
    {
        if (_disposed)
        {
            return;
        }

        if (!_strategyTasks.TryRemove(algoMetadata.StrategyId, out var taskInfo))
        {
            return;
        }

        taskInfo.TokenSource.Cancel();
    }

    private void StartStrategyTask(AlgoMetadata algoMeta)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken);
        var algo = AlgoFactory.Create(algoMeta);
        var reader = _orderBooksTopic.Subscribe();
        var wrapper = new PairTradingStrategyWrapper(algo, reader, _signalWriter, _loggerFactory);
        var task = wrapper.ExecuteAsync(cts.Token);

        var info = new TaskInfo
        {
            TokenSource = cts,
            ExecutingTask = task,
        };

        _strategyTasks.TryAdd(algoMeta.StrategyId, info);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _algoMetadataRepository.OnSaved -= OnAlgoSaved;
        _algoMetadataRepository.OnDeleted -= OnAlgoDeleted;

        try
        {
            var taskInfos = _strategyTasks.Values;
            _ = taskInfos.Select(ti => ti.TokenSource.CancelAsync());
            await Task.WhenAll(taskInfos.Select(ti => ti.ExecutingTask));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing PairTradingStrategyService. Message={Message}", ex.Message);
        }
    }
}
