namespace Vertr.Market.DataAccess.Tests;

[TestFixture(Category = "Database", Explicit = true)]
public class MarketTradeRepositoryTests : DataAccessTestBase
{
    private static readonly Guid Sber = new Guid("e6123145-9665-43e0-8413-cd61b8aa9b13");
    //private static readonly Guid Srm6 = new Guid("755574b5-703f-4a2f-9e1d-137c8cb90850"); // SRM6: SBRF-6.26 Сбер Банк (обыкновенные)
    //private static readonly Guid Sru6 = new Guid("f532beef-65bb-4a42-894c-e9bc728f93c4"); // SRU6: SBRF-9.26 Сбер Банк (обыкновенные)

    private static readonly DateTime From = new DateTime(2026, 03, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new DateTime(2026, 03, 24, 23, 59, 59, DateTimeKind.Utc);

    [Test]
    public async Task CanGetTrades()
    {
        var count = 0;
        await foreach (var trade in MarketTradeRepository.Get(Sber, From, To))
        {
            Console.WriteLine(trade);
            count++;

            if (count > 10)
            {
                break;
            }
        }
    }
}
