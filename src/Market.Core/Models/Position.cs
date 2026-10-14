namespace Market.Core.Models;

public class Position
{
    private readonly object _lock = new object();

    public ushort AssetId { get; init; }
    public PositionState State { get; private set; } = PositionState.None;
    public decimal Quantity { get; private set; } = 0;
    public decimal Price { get; private set; } = 0;
    public decimal Cash { get; private set; } = 0;

    /// <summary>
    /// Обновляет состояние позиции при исполнении новой сделки.
    /// </summary>
    /// <param name="tradeQty">Объем сделки (положительный для Buy, отрицательный для Sell)</param>
    /// <param name="tradePrice">Цена сделки (должна быть > 0)</param>
    public void Update(decimal tradeQty, decimal tradePrice)
    {
        if (tradePrice <= 0)
        {
            throw new ArgumentException("Цена сделки должна быть больше нуля.", nameof(tradePrice));
        }

        if (tradeQty == 0)
        {
            return;
        }

        lock (_lock)
        {
            Cash -= tradeQty * tradePrice;
            var newQty = Quantity + tradeQty;

            switch (State)
            {
                case PositionState.None:
                    // Открытие позиции с нуля
                    Price = tradePrice;
                    Quantity = newQty;
                    State = newQty > 0 ? PositionState.Long : PositionState.Short;
                    break;

                case PositionState.Long:
                    if (newQty > 0)
                    {
                        // Добор Long позиции -> пересчитываем среднюю цену только если объем увеличился
                        if (tradeQty > 0)
                        {
                            Price = ((Quantity * Price) + (tradeQty * tradePrice)) / newQty;
                        }

                        Quantity = newQty;
                    }
                    else if (newQty < 0)
                    {
                        // Разворот из Long в Short (переворот позиции)
                        Price = tradePrice;
                        Quantity = newQty;
                        State = PositionState.Short;
                    }
                    else
                    {
                        // Полное закрытие Long позиции
                        Price = 0;
                        Quantity = 0;
                        State = PositionState.None;
                    }

                    break;

                case PositionState.Short:
                    if (newQty < 0)
                    {
                        // Добор Short позиции -> пересчитываем среднюю цену (tradeQty в данном случае отрицательный)
                        if (tradeQty < 0)
                        {
                            Price = ((Quantity * Price) + (tradeQty * tradePrice)) / newQty;
                        }

                        Quantity = newQty;
                    }
                    else if (newQty > 0)
                    {
                        // Разворот из Short в Long (переворот позиции)
                        Price = tradePrice;
                        Quantity = newQty;
                        State = PositionState.Long;
                    }
                    else
                    {
                        // Полное закрытие Short позиции
                        Price = 0;
                        Quantity = 0;
                        State = PositionState.None;
                    }

                    break;
            }
        }
    }
}
