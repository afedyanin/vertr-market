
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace Market.ApiClient;

public static class MarketApiClientRegistrar
{
    public static IServiceCollection AddMarketTcpClient(this IServiceCollection services, string host, int port)
    {
        services.AddSingleton<ITcpClientConnection, TcpClientConnection>(sp => new TcpClientConnection(host, port));
        services.AddSingleton<IMarketTcpApiClient, MarketTcpApiClient>(sp => new MarketTcpApiClient(sp.GetRequiredService<ITcpClientConnection>()));

        return services;
    }
}
