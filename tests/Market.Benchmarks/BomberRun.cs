using Market.ApiClient;
using NBomber.CSharp;

namespace Market.Benchmarks;

internal sealed class BomberRun
{
    private readonly IMarketRestApiClient _restApiClient;
    private readonly Dictionary<ushort, MarketDepthGenerator> _generators;
    private readonly ushort _minKey;
    private readonly ushort _maxKey;

    public BomberRun(IMarketRestApiClient restApiClient, int assetCount = 150)
    {
        _restApiClient = restApiClient;
        _generators = InitGenerators(assetCount);
        _minKey = _generators.Keys.Min();
        _maxKey = _generators.Keys.Max();
    }

    public void ExecuteRestBenchmarkForBooks(int copies)
    {
        var scenario = Scenario.Create(
                "rest_benchmark_books",
                async context =>
                {
                    try
                    {
                        var assetId = (ushort)Random.Shared.Next(_minKey, _maxKey + 1);
                        var generator = _generators[assetId];
                        var book = generator.GenerateNext();

                        await _restApiClient.PostBooks([book]);
                        var saved = await _restApiClient.GetBooks(assetId, 1);

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
                    copies: copies,
                    during: TimeSpan.FromSeconds(30)));

        _ = NBomberRunner.RegisterScenarios(scenario).Run();
    }

    private Dictionary<ushort, MarketDepthGenerator> InitGenerators(int count)
    {
        var res = new Dictionary<ushort, MarketDepthGenerator>();
        ushort assetId = 1000;

        for (var i = 0; i < count; i++)
        {
            res[assetId] = new MarketDepthGenerator(assetId, 100 + Random.Shared.Next(200));
            assetId++;
        }

        return res;
    }
}
