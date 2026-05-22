namespace Vertr.Market.Application.Abstractions;

public interface IMarketDataStructEventPublisher<T> where T : struct
{
    public Task ExecuteAsync(CancellationToken stoppingToken);

    public void Update(int index, in T item);
}
