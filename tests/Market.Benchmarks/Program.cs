using Market.Core.Models;
using NBomber.Contracts;
using NBomber.CSharp;

namespace Market.Benchmarks;

internal static class Program
{
    private const int Copies = 10;
    private const int AssetsCount = 50;
    private const int BooksCount = 37;
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);

    // docker
    //private const int RestPort = 7001; // 5001 - localhost

    public static async Task Main(string[] args)
    {
        var scenarios = new List<ScenarioProps>();
        scenarios.Add(CreateRest());
        NBomberRunner.RegisterScenarios([.. scenarios]).Run();
    }

    private static ScenarioProps CreateRest()
    {
        var generators = OrderBookDepthGenerator.InitGenerators(AssetsCount);
        var minKey = generators.Keys.Min();
        var maxKey = generators.Keys.Max();

        var scenario = Scenario.Create(
                "rest_benchmark_books",
                async context =>
                {
                    try
                    {
                        var books = new OrderBookDepth[BooksCount];
                        var assetId = (ushort)Random.Shared.Next(minKey, maxKey + 1);
                        var generator = generators[assetId];

                        for (var i = 0; i < BooksCount; i++)
                        {
                            books[i] = generator.GenerateNext();
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
