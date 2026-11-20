using System.Threading.Channels;
using Market.Core.Models;
using Market.Core.Repositories;
using Market.Infrastructure;

namespace Market.Host.BackgroundServices;

public class PortfolioManagerService : BackgroundService
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly PositionReconciler _positionReconciler;
    private readonly ChannelReader<MyTrade> _tradesChannelReader;
    private readonly ILogger<PortfolioManagerService> _logger;

    public PortfolioManagerService(
        IPortfolioRepository portfolioRepository,
        PositionReconciler positionReconciler,
        Channel<MyTrade> tradesChannel,
        ILoggerFactory loggerFactory)
    {
        _portfolioRepository = portfolioRepository;
        _positionReconciler = positionReconciler;
        _tradesChannelReader = tradesChannel.Reader;
        _logger = loggerFactory.CreateLogger<PortfolioManagerService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"PositionManagerService started.");

        try
        {
            while (await _tradesChannelReader.WaitToReadAsync(stoppingToken))
            {
                while (_tradesChannelReader.TryRead(out var trade))
                {
                    try
                    {
                        var strategyId = trade.StrategyId ?? Guid.Empty;
                        var portfolio = _portfolioRepository.GetOrCreate(strategyId);
                        portfolio.Update(trade);

                        // Фактическая позиция важнее намерений стратегии:
                        // после каждой исполненной сделки сверяем её состояние.
                        _positionReconciler.TryReconcile(portfolio);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Trade processing failed for StrategyId={StrategyId}. Skipping trade TradeId={TradeId}.", trade.StrategyId, trade.TradeId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PositionManagerService cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PositionManagerService error. Message={Mesage}", ex.Message);
        }
        finally
        {
            _logger.LogInformation("PositionManagerService stopped.");
        }
    }
}
