using Tinkoff.InvestApi.V1;

namespace Market.Gateways.Tinvest.Helpers;

internal static class MoneyValueConverter
{
    public static MoneyValue Convert(this Money money)
    {
        var tmp = ToGoogleType(money);

        var destination = new MoneyValue()
        {
            Currency = tmp.CurrencyCode,
            Units = tmp.Units,
            Nano = tmp.Nanos,
        };

        return destination;
    }

    public static Money Convert(this MoneyValue source)
        => new Money(source, source.Currency);

    public static (decimal, string) FromMoneyValue(this MoneyValue source)
        => (source, source.Currency);

    internal static Google.Type.Money ToGoogleType(Money money)
        => new Google.Type.Money
        {
            CurrencyCode = money.Currency,
            DecimalValue = money.Value,
        };
}

public record class Money(decimal Value, string Currency = "rub");
