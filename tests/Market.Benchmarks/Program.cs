using Market.ApiClient;
using Refit;

namespace Market.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        var restApi = RestService.For<IMarketRestApiClient>("http://localhost:7001");
        var bomber = new BomberRun(restApi);
        bomber.ExecuteRestBenchmarkForBooks(5_000);
    }
}
