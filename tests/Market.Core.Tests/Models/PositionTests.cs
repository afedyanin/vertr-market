using Market.Core.Models;

namespace Market.Core.Tests.Models;

public class PositionTests
{
    [Test]
    public void CanProcessPosition()
    {
        var pos = new Position();
        PrintStatus(pos, "Инициализация");

        // 1. Покупаем 10 акций по цене 100$
        pos.Update(10, 100);
        PrintStatus(pos, "Buy 10 @ 100"); // Ожидаем Long, Qty: 10, Price: 100, Cash: -1000

        // 2. Докупаем еще 5 акций по цене 115$ (усреднение)
        pos.Update(5, 115);
        PrintStatus(pos, "Buy 5 @ 115");  // Ожидаем Long, Qty: 15, Price: 105, Cash: -1575

        // 3. Продаем 5 акций по цене 120$ (частичное закрытие)
        pos.Update(-5, 120);
        PrintStatus(pos, "Sell 5 @ 120"); // Ожидаем Long, Qty: 10, Price: 105 (не меняется), Cash: -975

        // 4. Продаем еще 10 акций по цене 130$ (выход в кэш)
        pos.Update(-10, 130);
        PrintStatus(pos, "Sell 10 @ 130"); // Ожидаем None, Qty: 0, Price: 0, Cash: +325 (чистая прибыль)

        // 5. Шортим 10 акций по цене 90$
        pos.Update(-10, 90);
        PrintStatus(pos, "Sell 10 @ 90");  // Ожидаем Short, Qty: -10, Price: 90, Cash: +1225

        // 6. Переворот: Покупаем сразу 15 акций по цене 95$
        pos.Update(15, 95);
        PrintStatus(pos, "Buy 15 @ 95");   // Ожидаем Long, Qty: 5, Price: 95, Cash: -200
    }

    [Test]
    public void CommissionReducesCashAndAccumulates()
    {
        // Arrange
        var pos = new Position();

        // Act
        pos.Update(10, 100, 1.5m);
        pos.Update(-4, 110, 0.5m);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(pos.Commission, Is.EqualTo(2m));
            Assert.That(pos.Cash, Is.EqualTo(-562m));
            Assert.That(pos.Quantity, Is.EqualTo(6m));
        });
    }

    [Test]
    public void NegativeCommission_Throws()
    {
        var pos = new Position();

        Assert.Throws<ArgumentOutOfRangeException>(() => pos.Update(1, 100, -0.01m));
    }

    private static void PrintStatus(Position position, string action)
    {
        Console.WriteLine($"После [{action}]: State={position.State,-5} | Qty={position.Quantity,-4} | AvgPrice={position.Price,-6} | Cash={position.Cash}");
    }
}
