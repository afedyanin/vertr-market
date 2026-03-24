using Microsoft.Extensions.DependencyInjection;
using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.DataAccess.Tests;

public abstract class DataAccessTestBase
{
    private const string LocalConnection = "Server=localhost;Port=5432;User Id=postgres;Password=admin;Database=market_data;";

    private readonly IServiceProvider _serviceProvider;

    protected IMarketTradeRepository MarketTradeRepository => _serviceProvider.GetRequiredService<IMarketTradeRepository>();

    protected IOrderBookRepository OrderBookRepository => _serviceProvider.GetRequiredService<IOrderBookRepository>();

    protected IOpenInterestRepository OpenInterestRepository => _serviceProvider.GetRequiredService<IOpenInterestRepository>();

    protected DataAccessTestBase()
    {
        var services = new ServiceCollection();
        services.AddMarketDataAccess(LocalConnection);
        _serviceProvider = services.BuildServiceProvider();
    }
}
