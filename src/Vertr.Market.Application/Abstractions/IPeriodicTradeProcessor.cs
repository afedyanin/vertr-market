using Vertr.Market.Application.Models;

namespace Vertr.Market.Application.Abstractions;

public interface IPeriodicTradeProcessor
{
    // Эта часть вызывается из фида, принимающего трейды
    public void HandleIncomingTrade(MarketTrade trade);

    // Эта часть вызвается из бэкграунд сервиса, периодически собирающего бары
    // и публикующего их в дисраптор
    public Task ExecuteAsync(CancellationToken cancellationToken = default);
}
