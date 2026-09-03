using Market.ApiClient;
using Market.Core.Abstractions;
using Market.Core.Models;

namespace Market.Gateways.Tinvest.Writers.RestApi;

internal sealed class MarketDepthRestWriter : IMarketDepthWriter
{
    private readonly IMarketRestApiClient _apiClient;

    public MarketDepthRestWriter(IMarketRestApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public void Write(in MarketDepth depth)
    {
    }
}
