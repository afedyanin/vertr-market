using Market.ApiClient;
using Market.ApiClient.Dtos;
using NBomber.Contracts;
using NBomber.CSharp;
using Refit;

namespace Market.Benchmarks;

internal static class Program
{
    private const int Copies = 10;
    private const int AssetsCount = 50;
    private const int BooksCount = 37;
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);

    // docker
    private const int RestPort = 7001; // 5001 - localhost

    public static async Task Main(string[] args)
    {
        var scenarios = new List<ScenarioProps>();
        scenarios.Add(CreateRest());
        NBomberRunner.RegisterScenarios([.. scenarios]).Run();
    }

    private static ScenarioProps CreateRest()
    {
        var restClient = RestService.For<IMarketRestApiClient>($"http://localhost:{RestPort}");
        var generators = MarketDepthGenerator.InitGenerators(AssetsCount);
        var minKey = generators.Keys.Min();
        var maxKey = generators.Keys.Max();

        var scenario = Scenario.Create(
                "rest_benchmark_books",
                async context =>
                {
                    try
                    {
                        var books = new MarketDepthDto[BooksCount];
                        var assetId = (ushort)Random.Shared.Next(minKey, maxKey + 1);
                        var generator = generators[assetId];

                        for (var i = 0; i < BooksCount; i++)
                        {
                            books[i] = generator.GenerateNext();
                        }

                        await restClient.PostBooks(books);
                        var saved = await restClient.GetBooks(assetId, 2);

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
