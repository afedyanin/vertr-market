using Market.ApiClient;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Internals;
using NBomber.Contracts;
using NBomber.CSharp;
using Refit;

namespace Market.Benchmarks;

internal static class Program
{
    private const int Copies = 10_000;
    private const int AssetCount = 150;
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    public static async Task Main(string[] args)
    {
        var tcpConnction = new TcpClientConnection("localhost", 8005);
        await tcpConnction.ConnectAsync();
        var tcpClient = new MarketTcpApiClient(tcpConnction);
        var tcpScenatio = CreateTcp(tcpClient);

        var restClient = RestService.For<IMarketRestApiClient>("http://localhost:5001");
        var restScenatio = CreateRest(restClient);

        NBomberRunner.RegisterScenarios([restScenatio, tcpScenatio]).Run();
    }

    private static ScenarioProps CreateRest(IMarketRestApiClient restClient)
    {
        var generators = MarketDepthGenerator.InitGenerators(AssetCount);
        var minKey = generators.Keys.Min();
        var maxKey = generators.Keys.Max();

        var scenario = Scenario.Create(
                "rest_benchmark_books",
                async context =>
                {
                    try
                    {
                        var assetId = (ushort)Random.Shared.Next(minKey, maxKey + 1);
                        var generator = generators[assetId];
                        var book = generator.GenerateNext();

                        await restClient.PostBooks([book]);
                        var saved = await restClient.GetBooks(assetId, 1);

                        if (saved is null || saved.Length == 0)
                        {
                            return Response.Fail(message: "GET returned empty result", statusCode: "500");
                        }

                        return Response.Ok();
                    }
                    catch (Exception ex)
                    {
                        return Response.Fail(message: ex.Message, statusCode: "500");
                    }
                })
            .WithWarmUpDuration(TimeSpan.FromSeconds(1))
            .WithLoadSimulations(
                Simulation.KeepConstant(
                    copies: Copies,
                    during: Duration));

        return scenario;
    }

    private static ScenarioProps CreateTcp(IMarketTcpApiClient tcpClient)
    {
        var generators = MarketDepthGenerator.InitGenerators(AssetCount);
        var minKey = generators.Keys.Min();
        var maxKey = generators.Keys.Max();

        var scenario = Scenario.Create(
                "tcp_benchmark_books",
                async context =>
                {
                    try
                    {
                        var assetId = (ushort)Random.Shared.Next(minKey, maxKey + 1);
                        var generator = generators[assetId];
                        var book = generator.GenerateNext();

                        await tcpClient.PostBooks([book]);
                        var saved = await tcpClient.GetBooks(assetId, 1);

                        if (saved is null || saved.Length == 0)
                        {
                            return Response.Fail(message: "GET returned empty result", statusCode: "500");
                        }

                        return Response.Ok();
                    }
                    catch (Exception ex)
                    {
                        return Response.Fail(message: ex.Message, statusCode: "500");
                    }
                })
            .WithWarmUpDuration(TimeSpan.FromSeconds(1))
            .WithLoadSimulations(
                Simulation.KeepConstant(
                    copies: Copies,
                    during: Duration));

        return scenario;
    }
}
