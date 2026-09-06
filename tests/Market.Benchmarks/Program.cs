using Market.ApiClient;
using Market.ApiClient.Tcp;
using Market.ApiClient.Tcp.Internals;
using NBomber.Contracts;
using NBomber.CSharp;
using Refit;

namespace Market.Benchmarks;

internal static class Program
{
    private const int Copies = 10;
    private const int AssetCount = 50;
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(30);

    public static async Task Main(string[] args)
    {
        var scenarios = new List<ScenarioProps>
        {
            CreateRest(),
            CreateTcp(),
        };

        NBomberRunner.RegisterScenarios([.. scenarios]).Run();
    }

    private static ScenarioProps CreateRest()
    {
        var restClient = RestService.For<IMarketRestApiClient>("http://localhost:5001");
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
            .WithLoadSimulations(
                Simulation.KeepConstant(
                    copies: Copies,
                    during: Duration));

        return scenario;
    }

    private static ScenarioProps CreateTcp()
    {
        var tcpConnction = new TcpClientConnection("localhost", 8005);
        var tcpClient = new MarketTcpApiClient(tcpConnction);

        var generators = MarketDepthGenerator.InitGenerators(AssetCount);
        var minKey = generators.Keys.Min();
        var maxKey = generators.Keys.Max();

        var scenario = Scenario.Create(
                "tcp_benchmark_books",
                async context =>
                {
                    try
                    {
                        await tcpConnction.ConnectAsync(CancellationToken.None);

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
            .WithLoadSimulations(
                Simulation.KeepConstant(
                    copies: Copies,
                    during: Duration));

        return scenario;
    }
}
