using Vertr.Market.Application.Abstractions;

namespace Vertr.Market.Application.Models;

public class Bar : IResetable<Bar>
{
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }
    public double Value { get; set; } // Оборот (Price * Quantity)

    public bool IsEmpty { get; set; } = true;

    // Метод для Lock-Free агрегации одной входящей сделки
    public void AggregateTrade(double price, double quantity)
    {
        // Если это первая сделка в текущем баре
        if (IsEmpty)
        {
            Open = price;
            High = price;
            Low = price;
            Close = price;
            Volume = quantity;
            Value = price * quantity;
            IsEmpty = false;
            return;
        }

        // Обновляем экстремумы
        if (price > High)
        {
            High = price;
        }

        if (price < Low)
        {
            Low = price;
        }

        // Последняя сделка формирует цену закрытия
        Close = price;

        // Накапливаем объемы
        Volume += quantity;
        Value += (price * quantity);
    }

    public void CopyFrom(Bar source)
    {
        Open = source.Open;
        High = source.High;
        Low = source.Low;
        Close = source.Close;
        Volume = source.Volume;
        Value = source.Value;
        IsEmpty = source.IsEmpty;
    }

    public void Reset()
    {
        Open = High = Low = Close = Volume = Value = 0;
        IsEmpty = true;
    }
}