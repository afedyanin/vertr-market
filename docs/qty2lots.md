
Есть два актива, заданные количеством в штуках: qtyX и qtyY (могут быть дробные числа). 
Задана также лотность каждого актива (количество штук в одном лоте): lotSizeX, lotSizeY. 

Нужно пересчитать количество из штук в лоты так, чтобы:
- получилось минимально необходимое количество лотов по каждому активу
- сохранилось соотношение (пропорция) по количеству между qtyX и qtyY.

qtyY фиксирован и задается пользователем, qtyX берется из соотношения qtyX = qtyY / beta. Где beta - коэффициент из фильтра Калмана


========================

При фиксированном qtyY и динамическом beta из фильтра Калмана нельзя сокращать пропорцию на НОД. 
Вместо этого нужно перевести qtyY в целое число лотов lotsY и вычислить ближайшее целое lotsX по формуле: 

lotsX = Math.Round(lotsY * lotSizeY / (beta * lotSizeX)).

Почему подход меняется

- Фиксация объема. Поскольку qtyY задан пользователем, позиция по активу Y не может быть произвольно увеличена или уменьшена для подгонки под целые дроби.
- Природа коэффициента Калмана. Фильтр Калмана выдает непрерывное число с плавающей точкой (beta), которое меняется на каждом тике или баре. 
Точное сокращение дробей здесь невозможно и бессмысленно.
- Округление до целого лота. Единственный способ сохранить хедж при фиксированном Y — перевести Y в лоты и округлить расчетное количество лотов X до ближайшего целого.

Реализация на C#

Ниже приведено решение, которое переводит заданный qtyY в лоты, рассчитывает qtyX через beta и возвращает итоговые целые лоты, 
а также фактическую погрешность хеджирования (насколько реальная позиция отклоняется от идеальной из-за дискретности лотов).

``` cs

csharp
using System;

public readonly record struct PairPositionResult(
    int LotsX,
    int LotsY,
    decimal ActualQtyX,
    decimal ActualQtyY,
    double HedgeErrorPercent);

public class KalmanLotCalculator
{
    /// <summary>
    /// Рассчитывает количество лотов для пары при фиксированном qtyY и бете из фильтра Калмана.
    /// </summary>
    /// <param name="qtyY">Заданное пользователем количество штук актива Y.</param>
    /// <param name="beta">Коэффициент хеджирования из фильтра Калмана (qtyX = qtyY / beta).</param>
    /// <param name="lotSizeX">Количество штук в 1 лоте актива X.</param>
    /// <param name="lotSizeY">Количество штук в 1 лоте актива Y.</param>
    /// <param name="allowZeroX">Разрешать ли 0 лотов по X, если расчетное значение меньше половины лота.</param>
    public static PairPositionResult CalculateLots(
        decimal qtyY,
        double beta,
        decimal lotSizeX,
        decimal lotSizeY,
        bool allowZeroX = false)
    {
        if (qtyY <= 0)
            throw new ArgumentException("qtyY должен быть больше нуля.", nameof(qtyY));
        if (beta <= 0 || double.IsNaN(beta) || double.IsInfinity(beta))
            throw new ArgumentException("Некорректное значение beta.", nameof(beta));
        if (lotSizeX <= 0 || lotSizeY <= 0)
            throw new ArgumentException("Лотность должна быть больше нуля.");

        // 1. Округляем заданный qtyY до ближайшего целого количества лотов (минимум 1 лот)
        int lotsY = Math.Max(1, (int)Math.Round(qtyY / lotSizeY, MidpointRounding.AwayFromZero));
        decimal actualQtyY = lotsY * lotSizeY;

        // 2. Вычисляем идеальное количество штук X исходя из фактического количества Y
        // Используем actualQtyY вместо исходного qtyY, чтобы сохранить точное соотношение к реальной позиции
        decimal idealQtyX = actualQtyY / (decimal)beta;

        // 3. Переводим идеальный qtyX в целые лоты
        decimal rawLotsX = idealQtyX / lotSizeX;
        int lotsX = (int)Math.Round(rawLotsX, MidpointRounding.AwayFromZero);

        if (!allowZeroX && lotsX == 0)
            lotsX = 1;

        decimal actualQtyX = lotsX * lotSizeX;

        // 4. Считаем относительную ошибку округления (отклонение от идеального хеджа в процентах)
        double errorPercent = idealQtyX > 0
            ? (double)(Math.Abs(actualQtyX - idealQtyX) / idealQtyX) * 100.0
            : 0.0;

        return new PairPositionResult(lotsX, lotsY, actualQtyX, actualQtyY, errorPercent);
    }
}
```

Важные нюансы для торговли по фильтру Калмана

- Расчет от actualQtyY. В коде выше idealQtyX считается от уже округленного actualQtyY, а не от сырого qtyY. Если пользователь ввел qtyY = 14, а размер лота lotSizeY = 10, в рынок уйдет 1 лот (10 штук), поэтому ногу X нужно хеджировать именно под 10 штук, а не под 14.
- Контроль ошибки округления (HedgeErrorPercent). Если qtyY слишком мал, округление lotsX до целого может сильно исказить пропорцию (например, вместо 1.4 лота купится 1 лот — ошибка ~28%). Поле HedgeErrorPercent позволяет отфильтровать сделки, где точность хеджа недопустимо падает.
- Знак коэффициента beta. В парном трейдинге beta обычно положительна (один актив покупается, второй продается). Если ваша модель Калмана допускает отрицательную бету (однонаправленные позиции), уберите проверку beta <= 0 и используйте Math.Abs(beta) для расчета количества лотов, сохраняя знак для направления ордера.

=================

Ниже приведены юнит-тесты на фреймворке NUnit для класса KalmanLotCalculator. 
Тесты покрывают ваши два сценария (с подробным разбором математики округления), а также проверяют краевые случаи и обработку некорректных данных.


``` csharp

using System;
using NUnit.Framework;

[TestFixture]
public class KalmanLotCalculatorTests
{
    [Test]
    public void CalculateLots_Case1_LotSizeYGreaterThanLotSizeX_ReturnsExpectedLots()
    {
        // Arrange
        // qtyY = 1 при lotSizeY = 100 округляется до минимума 1 лота (actualQtyY = 100 шт).
        // idealQtyX = 100 / 1.13 ≈ 88.4956 шт.
        // rawLotsX = 88.4956 / 10 ≈ 8.8496 лотов -> округляется до 9 лотов (actualQtyX = 90 шт).
        decimal qtyY = 1m;
        double beta = 1.13;
        decimal lotSizeX = 10m;
        decimal lotSizeY = 100m;

        // Act
        var result = KalmanLotCalculator.CalculateLots(qtyY, beta, lotSizeX, lotSizeY);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.LotsY, Is.EqualTo(1));
            Assert.That(result.ActualQtyY, Is.EqualTo(100m));
            Assert.That(result.LotsX, Is.EqualTo(9));
            Assert.That(result.ActualQtyX, Is.EqualTo(90m));
            // Ошибка: |90 - 88.495575| / 88.495575 * 100 ≈ 1.70%
            Assert.That(result.HedgeErrorPercent, Is.EqualTo(1.70).Within(0.01));
        });
    }

    [Test]
    public void CalculateLots_Case2_LotSizeXGreaterThanLotSizeY_ReturnsMinimumOneLotX()
    {
        // Arrange
        // qtyY = 1 при lotSizeY = 10 округляется до минимума 1 лота (actualQtyY = 10 шт).
        // idealQtyX = 10 / 1.13 ≈ 8.8496 шт.
        // rawLotsX = 8.8496 / 100 ≈ 0.0885 лотов.
        // При allowZeroX = false (по умолчанию) округляется вверх до 1 лота (actualQtyX = 100 шт).
        decimal qtyY = 1m;
        double beta = 1.13;
        decimal lotSizeX = 100m;
        decimal lotSizeY = 10m;

        // Act
        var result = KalmanLotCalculator.CalculateLots(qtyY, beta, lotSizeX, lotSizeY, allowZeroX: false);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.LotsY, Is.EqualTo(1));
            Assert.That(result.ActualQtyY, Is.EqualTo(10m));
            Assert.That(result.LotsX, Is.EqualTo(1));
            Assert.That(result.ActualQtyX, Is.EqualTo(100m));
            // Огромная ошибка хеджа (~1030%), так как 1 лот X слишком велик для 1 лота Y
            Assert.That(result.HedgeErrorPercent, Is.EqualTo(1030.0).Within(0.1));
        });
    }

    [Test]
    public void CalculateLots_Case2WithAllowZeroX_ReturnsZeroLotsX()
    {
        // Arrange: тот же сценарий, но разрешаем 0 лотов по X, если позиция слишком мала
        decimal qtyY = 1m;
        double beta = 1.13;
        decimal lotSizeX = 100m;
        decimal lotSizeY = 10m;

        // Act
        var result = KalmanLotCalculator.CalculateLots(qtyY, beta, lotSizeX, lotSizeY, allowZeroX: true);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.LotsY, Is.EqualTo(1));
            Assert.That(result.LotsX, Is.EqualTo(0));
            Assert.That(result.ActualQtyX, Is.EqualTo(0m));
            Assert.That(result.HedgeErrorPercent, Is.EqualTo(100.0).Within(0.01));
        });
    }

    [TestCase(14.9, 10, 1, 10)] // 1.49 лота -> 1 лот (10 шт)
    [TestCase(15.0, 10, 2, 20)] // 1.50 лота -> 2 лота (MidpointRounding.AwayFromZero)
    [TestCase(25.0, 10, 3, 30)] // 2.50 лота -> 3 лота (проверка отсутствия банковского округления)
    public void CalculateLots_FractionalQtyY_RoundsMidpointAwayFromZero(
        decimal qtyY, decimal lotSizeY, int expectedLotsY, decimal expectedActualQtyY)
    {
        // Act (при beta = 1.0 и lotSizeX = lotSizeY лоты X и Y совпадают)
        var result = KalmanLotCalculator.CalculateLots(qtyY, beta: 1.0, lotSizeX: lotSizeY, lotSizeY: lotSizeY);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.LotsY, Is.EqualTo(expectedLotsY));
            Assert.That(result.ActualQtyY, Is.EqualTo(expectedActualQtyY));
            Assert.That(result.LotsX, Is.EqualTo(expectedLotsY));
            Assert.That(result.HedgeErrorPercent, Is.EqualTo(0.0).Within(1e-9));
        });
    }

    [Test]
    public void CalculateLots_FractionalLotSizes_CryptoOrForexAssets_CalculatesCorrectly()
    {
        // Arrange: дробная лотность (например, 0.01 лота для криптовалют или CFD)
        decimal qtyY = 0.55m;
        double beta = 2.0;
        decimal lotSizeX = 0.01m;
        decimal lotSizeY = 0.1m;

        // Act
        // qtyY = 0.55 / 0.1 = 5.5 -> 6 лотов Y (actualQtyY = 0.6)
        // idealQtyX = 0.6 / 2.0 = 0.3 -> 0.3 / 0.01 = 30 лотов X
        var result = KalmanLotCalculator.CalculateLots(qtyY, beta, lotSizeX, lotSizeY);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.LotsY, Is.EqualTo(6));
            Assert.That(result.ActualQtyY, Is.EqualTo(0.6m));
            Assert.That(result.LotsX, Is.EqualTo(30));
            Assert.That(result.ActualQtyX, Is.EqualTo(0.30m));
            Assert.That(result.HedgeErrorPercent, Is.EqualTo(0.0).Within(1e-9));
        });
    }

    [TestCase(0, 1.13, 10, 100)]
    [TestCase(-5, 1.13, 10, 100)]
    [TestCase(10, 0, 10, 100)]
    [TestCase(10, -1.5, 10, 100)]
    [TestCase(10, double.NaN, 10, 100)]
    [TestCase(10, double.PositiveInfinity, 10, 100)]
    [TestCase(10, 1.13, 0, 100)]
    [TestCase(10, 1.13, 10, -10)]
    public void CalculateLots_InvalidInputs_ThrowsArgumentException(
        decimal qtyY, double beta, decimal lotSizeX, decimal lotSizeY)
    {
        Assert.Throws<ArgumentException>(() =>
            KalmanLotCalculator.CalculateLots(qtyY, beta, lotSizeX, lotSizeY));
    }
}

```

На что обратить внимание в ваших тестовых сценариях

- Сценарий 1 (lotSizeX = 10, lotSizeY = 100). Погрешность хеджирования составляет всего 1.70%, так как мелкий шаг лота по X позволяет достаточно точно подстроиться под 1 крупный лот Y.
- Сценарий 2 (lotSizeX = 100, lotSizeY = 10). При qtyY = 1 минимальный объем X в 1 лот (100 штук) в 11 раз превышает расчетный (8.85 штук), что дает ошибку 1030%. В боевом роботе стоит добавить проверку на максимально допустимый HedgeErrorPercent (например, не входить в сделку, если ошибка больше 5–10%).

======


Хотите, чтобы я дополнил калькулятор методом автоматического подбора минимального qtyY, при котором ошибка хеджирования гарантированно не превышает заданный вами порог (например, 2%)?

Ниже приведено дополнение для KalmanLotCalculator с методом автоматического подбора минимального объема позиции (FindMinimumValidPosition), а также юнит-тесты для него.

Как работает алгоритм подбора

Метод начинает с базового значения lotsY (полученного из minQtyY) и последовательно увеличивает его по 1 лоту, пока относительная ошибка округления по ноге X не станет меньше или равна заданному порогу (maxErrorPercent). Если приемлемый баланс не найден в пределах лимита maxLotsY, метод выбросит исключение или вернет лучший найденный вариант.

Обновленный класс KalmanLotCalculator

``` csharp

using System;

public readonly record struct PairPositionResult(
    int LotsX,
    int LotsY,
    decimal ActualQtyX,
    decimal ActualQtyY,
    double HedgeErrorPercent);

public class KalmanLotCalculator
{
    public static PairPositionResult CalculateLots(
        decimal qtyY,
        double beta,
        decimal lotSizeX,
        decimal lotSizeY,
        bool allowZeroX = false)
    {
        if (qtyY <= 0)
            throw new ArgumentException("qtyY должен быть больше нуля.", nameof(qtyY));
        if (beta <= 0 || double.IsNaN(beta) || double.IsInfinity(beta))
            throw new ArgumentException("Некорректное значение beta.", nameof(beta));
        if (lotSizeX <= 0 || lotSizeY <= 0)
            throw new ArgumentException("Лотность должна быть больше нуля.");

        int lotsY = Math.Max(1, (int)Math.Round(qtyY / lotSizeY, MidpointRounding.AwayFromZero));
        return CalculateFromLotsY(lotsY, beta, lotSizeX, lotSizeY, allowZeroX);
    }

    /// <summary>
    /// Подбирает минимально необходимый объем позиции (начиная с minQtyY),
    /// при котором ошибка хеджирования не превышает maxErrorPercent.
    /// </summary>
    /// <param name="minQtyY">Желаемый минимальный объем актива Y в штуках.</param>
    /// <param name="beta">Коэффициент хеджирования из фильтра Калмана.</param>
    /// <param name="lotSizeX">Лотность актива X.</param>
    /// <param name="lotSizeY">Лотность актива Y.</param>
    /// <param name="maxErrorPercent">Максимально допустимая ошибка округления X в процентах (например, 2.0 для 2%).</param>
    /// <param name="maxLotsY">Ограничение сверху на количество лотов Y (защита от перерасхода депозита).</param>
    public static PairPositionResult FindMinimumValidPosition(
        decimal minQtyY,
        double beta,
        decimal lotSizeX,
        decimal lotSizeY,
        double maxErrorPercent = 2.0,
        int maxLotsY = 10_000)
    {
        if (minQtyY <= 0)
            throw new ArgumentException("minQtyY должен быть больше нуля.", nameof(minQtyY));
        if (beta <= 0 || double.IsNaN(beta) || double.IsInfinity(beta))
            throw new ArgumentException("Некорректное значение beta.", nameof(beta));
        if (lotSizeX <= 0 || lotSizeY <= 0)
            throw new ArgumentException("Лотность должна быть больше нуля.");
        if (maxErrorPercent < 0)
            throw new ArgumentException("Допустимая ошибка не может быть отрицательной.", nameof(maxErrorPercent));
        if (maxLotsY < 1)
            throw new ArgumentException("maxLotsY должен быть не меньше 1.", nameof(maxLotsY));

        int startLotsY = Math.Max(1, (int)Math.Round(minQtyY / lotSizeY, MidpointRounding.AwayFromZero));

        for (int lotsY = startLotsY; lotsY <= maxLotsY; lotsY++)
        {
            var candidate = CalculateFromLotsY(lotsY, beta, lotSizeX, lotSizeY, allowZeroX: false);
            if (candidate.HedgeErrorPercent <= maxErrorPercent)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Не удалось подобрать позицию с ошибкой <= {maxErrorPercent}% в пределах {maxLotsY} лотов Y.");
    }

    private static PairPositionResult CalculateFromLotsY(
        int lotsY,
        double beta,
        decimal lotSizeX,
        decimal lotSizeY,
        bool allowZeroX)
    {
        decimal actualQtyY = lotsY * lotSizeY;
        decimal idealQtyX = actualQtyY / (decimal)beta;

        decimal rawLotsX = idealQtyX / lotSizeX;
        int lotsX = (int)Math.Round(rawLotsX, MidpointRounding.AwayFromZero);

        if (!allowZeroX && lotsX == 0)
            lotsX = 1;

        decimal actualQtyX = lotsX * lotSizeX;

        double errorPercent = idealQtyX > 0
            ? (double)(Math.Abs(actualQtyX - idealQtyX) / idealQtyX) * 100.0
            : 0.0;

        return new PairPositionResult(lotsX, lotsY, actualQtyX, actualQtyY, errorPercent);
    }
}

```

Юнит-тесты для подбора минимальной позиции (NUnit)

На примере вашего второго сценария (lotSizeX = 100, lotSizeY = 10, beta = 1.13), где ошибка при 1 лоте составляла 1030%, алгоритм автоматически увеличит позицию Y до 11 лотов (110 штук). Это даст идеальный объем X в 97.35 штук, который округлится до 1 лота (100 штук) с итоговой погрешностью всего 2.73%.

``` csharp
using System;
using NUnit.Framework;

[TestFixture]
public class KalmanAutoSizingTests
{
    [Test]
    public void FindMinimumValidPosition_Case2With3PercentThreshold_ScalesUpToValidHedge()
    {
        // Arrange
        // Исходно при 1 лоте Y ошибка была 1030%.
        // При 11 лотах Y (110 шт): idealQtyX = 110 / 1.13 ≈ 97.345 шт.
        // Округляется до 1 лота X (100 шт). Ошибка = |100 - 97.345| / 97.345 ≈ 2.727%.
        decimal minQtyY = 1m;
        double beta = 1.13;
        decimal lotSizeX = 100m;
        decimal lotSizeY = 10m;
        double maxError = 3.0;

        // Act
        var result = KalmanLotCalculator.FindMinimumValidPosition(
            minQtyY, beta, lotSizeX, lotSizeY, maxErrorPercent: maxError);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.LotsY, Is.EqualTo(11));
            Assert.That(result.ActualQtyY, Is.EqualTo(110m));
            Assert.That(result.LotsX, Is.EqualTo(1));
            Assert.That(result.ActualQtyX, Is.EqualTo(100m));
            Assert.That(result.HedgeErrorPercent, Is.LessThanOrEqualTo(maxError));
            Assert.That(result.HedgeErrorPercent, Is.EqualTo(2.73).Within(0.01));
        });
    }

    [Test]
    public void FindMinimumValidPosition_Case1AlreadyValid_DoesNotIncreaseLotsY()
    {
        // Arrange
        // В первом сценарии при 1 лоте Y ошибка 1.70%, что уже укладывается в порог 2.0%
        decimal minQtyY = 1m;
        double beta = 1.13;
        decimal lotSizeX = 10m;
        decimal lotSizeY = 100m;

        // Act
        var result = KalmanLotCalculator.FindMinimumValidPosition(
            minQtyY, beta, lotSizeX, lotSizeY, maxErrorPercent: 2.0);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.LotsY, Is.EqualTo(1));
            Assert.That(result.LotsX, Is.EqualTo(9));
            Assert.That(result.HedgeErrorPercent, Is.LessThanOrEqualTo(2.0));
        });
    }

    [Test]
    public void FindMinimumValidPosition_ImpossibleThresholdWithinLimit_ThrowsInvalidOperationException()
    {
        // Arrange: требуем нереальную точность 0.0001% при лимите всего в 5 лотов
        Assert.Throws<InvalidOperationException>(() =>
            KalmanLotCalculator.FindMinimumValidPosition(
                minQtyY: 1m,
                beta: 1.13,
                lotSizeX: 100m,
                lotSizeY: 10m,
                maxErrorPercent: 0.0001,
                maxLotsY: 5));
    }
}
```

Практический совет для боевого робота

-  Контроль капитала. Поскольку FindMinimumValidPosition может увеличить LotsY в несколько раз (как в тесте — с 1 до 11 лотов), перед отправкой ордеров обязательно проверяйте итоговую стоимость обеих ног (ActualQtyX * PriceX + ActualQtyY * PriceY) на соответствие доступному гарантийному обеспечению или депозиту.

Нужно ли добавить в метод ограничение по максимальному бюджету в деньгах (передавая текущие цены priceX и priceY), чтобы подбор останавливался, если позиция выходит за рамки выделенного на пару капитала?

========================

Ниже приведена обновленная версия класса KalmanLotCalculator, в которую добавлен контроль максимального бюджета в деньгах (maxTotalBudget) с учетом текущих цен активов (priceX, priceY), а также юнит-тесты для проверки этого ограничения.

Как работает проверка бюджета

На каждой итерации подбора lotsY алгоритм вычисляет суммарную стоимость обеих ног позиции: TotalCost = ActualQtyX * priceX + ActualQtyY * priceY. Как только стоимость превышает выделенный бюджет, цикл останавливается. Если к этому моменту подходящая по точности комбинация не найдена, метод выбрасывает исключение с указанием требуемого бюджета.

Обновленный код KalmanLotCalculator

``` csharp

using System;

public readonly record struct PairPositionResult(
    int LotsX,
    int LotsY,
    decimal ActualQtyX,
    decimal ActualQtyY,
    decimal TotalCost,
    double HedgeErrorPercent);

public class KalmanLotCalculator
{
    public static PairPositionResult CalculateLots(
        decimal qtyY,
        double beta,
        decimal lotSizeX,
        decimal lotSizeY,
        decimal priceX = 0m,
        decimal priceY = 0m,
        bool allowZeroX = false)
    {
        ValidateCommonInputs(qtyY, beta, lotSizeX, lotSizeY, priceX, priceY);

        int lotsY = Math.Max(1, (int)Math.Round(qtyY / lotSizeY, MidpointRounding.AwayFromZero));
        return CalculateFromLotsY(lotsY, beta, lotSizeX, lotSizeY, priceX, priceY, allowZeroX);
    }

    /// <summary>
    /// Подбирает минимальный объем позиции с ошибкой <= maxErrorPercent,
    /// не выходящий за рамки maxTotalBudget и maxLotsY.
    /// </summary>
    public static PairPositionResult FindMinimumValidPosition(
        decimal minQtyY,
        double beta,
        decimal lotSizeX,
        decimal lotSizeY,
        decimal priceX,
        decimal priceY,
        decimal maxTotalBudget,
        double maxErrorPercent = 2.0,
        int maxLotsY = 10_000)
    {
        ValidateCommonInputs(minQtyY, beta, lotSizeX, lotSizeY, priceX, priceY);

        if (priceX <= 0 || priceY <= 0)
            throw new ArgumentException("Цены активов должны быть больше нуля для контроля бюджета.");
        if (maxTotalBudget <= 0)
            throw new ArgumentException("Бюджет должен быть больше нуля.", nameof(maxTotalBudget));
        if (maxErrorPercent < 0)
            throw new ArgumentException("Допустимая ошибка не может быть отрицательной.", nameof(maxErrorPercent));
        if (maxLotsY < 1)
            throw new ArgumentException("maxLotsY должен быть не меньше 1.", nameof(maxLotsY));

        int startLotsY = Math.Max(1, (int)Math.Round(minQtyY / lotSizeY, MidpointRounding.AwayFromZero));

        for (int lotsY = startLotsY; lotsY <= maxLotsY; lotsY++)
        {
            var candidate = CalculateFromLotsY(lotsY, beta, lotSizeX, lotSizeY, priceX, priceY, allowZeroX: false);

            // Если стоимость позиции превысила выделенный бюджет, дальнейший рост lotsY не имеет смысла
            if (candidate.TotalCost > maxTotalBudget)
            {
                throw new InvalidOperationException(
                    $"Превышен бюджет {maxTotalBudget} на шаге {lotsY} лотов Y (стоимость {candidate.TotalCost}, ошибка {candidate.HedgeErrorPercent:F2}%).");
            }

            if (candidate.HedgeErrorPercent <= maxErrorPercent)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Не удалось подобрать позицию с ошибкой <= {maxErrorPercent}% в пределах {maxLotsY} лотов Y.");
    }

    private static PairPositionResult CalculateFromLotsY(
        int lotsY,
        double beta,
        decimal lotSizeX,
        decimal lotSizeY,
        decimal priceX,
        decimal priceY,
        bool allowZeroX)
    {
        decimal actualQtyY = lotsY * lotSizeY;
        decimal idealQtyX = actualQtyY / (decimal)beta;

        decimal rawLotsX = idealQtyX / lotSizeX;
        int lotsX = (int)Math.Round(rawLotsX, MidpointRounding.AwayFromZero);

        if (!allowZeroX && lotsX == 0)
            lotsX = 1;

        decimal actualQtyX = lotsX * lotSizeX;
        decimal totalCost = (actualQtyX * priceX) + (actualQtyY * priceY);

        double errorPercent = idealQtyX > 0
            ? (double)(Math.Abs(actualQtyX - idealQtyX) / idealQtyX) * 100.0
            : 0.0;

        return new PairPositionResult(lotsX, lotsY, actualQtyX, actualQtyY, totalCost, errorPercent);
    }

    private static void ValidateCommonInputs(
        decimal qtyY, double beta, decimal lotSizeX, decimal lotSizeY, decimal priceX, decimal priceY)
    {
        if (qtyY <= 0)
            throw new ArgumentException("qtyY должен быть больше нуля.", nameof(qtyY));
        if (beta <= 0 || double.IsNaN(beta) || double.IsInfinity(beta))
            throw new ArgumentException("Некорректное значение beta.", nameof(beta));
        if (lotSizeX <= 0 || lotSizeY <= 0)
            throw new ArgumentException("Лотность должна быть больше нуля.");
        if (priceX < 0 || priceY < 0)
            throw new ArgumentException("Цены не могут быть отрицательными.");
    }
}

```

Юнит-тесты для контроля бюджета (NUnit)

``` csharp
using System;
using NUnit.Framework;

[TestFixture]
public class KalmanBudgetTests
{
    [Test]
    public void FindMinimumValidPosition_WithinBudget_ReturnsValidPositionAndExactCost()
    {
        // Arrange
        // Для достижения ошибки <= 3% нужно 11 лотов Y (110 шт) и 1 лот X (100 шт).
        // При priceX = 50 и priceY = 45 стоимость составит: 100 * 50 + 110 * 45 = 5000 + 4950 = 9950.
        decimal minQtyY = 1m;
        double beta = 1.13;
        decimal lotSizeX = 100m;
        decimal lotSizeY = 10m;
        decimal priceX = 50m;
        decimal priceY = 45m;
        decimal budget = 10_000m;

        // Act
        var result = KalmanLotCalculator.FindMinimumValidPosition(
            minQtyY, beta, lotSizeX, lotSizeY, priceX, priceY, budget, maxErrorPercent: 3.0);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.LotsY, Is.EqualTo(11));
            Assert.That(result.LotsX, Is.EqualTo(1));
            Assert.That(result.TotalCost, Is.EqualTo(9950m));
            Assert.That(result.TotalCost, Is.LessThanOrEqualTo(budget));
            Assert.That(result.HedgeErrorPercent, Is.LessThanOrEqualTo(3.0));
        });
    }

    [Test]
    public void FindMinimumValidPosition_BudgetExceededBeforeReachingAccuracy_ThrowsInvalidOperationException()
    {
        // Arrange: требуемая позиция стоит 9950, но бюджет ограничен суммой 8000
        decimal minQtyY = 1m;
        double beta = 1.13;
        decimal lotSizeX = 100m;
        decimal lotSizeY = 10m;
        decimal priceX = 50m;
        decimal priceY = 45m;
        decimal tightBudget = 8_000m;

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            KalmanLotCalculator.FindMinimumValidPosition(
                minQtyY, beta, lotSizeX, lotSizeY, priceX, priceY, tightBudget, maxErrorPercent: 3.0));

        Assert.That(ex.Message, Does.Contain("Превышен бюджет"));
    }

    [Test]
    public void FindMinimumValidPosition_InitialStepAlreadyExceedsBudget_ThrowsImmediately()
    {
        // Arrange: даже 1 лот X (100 * 50 = 5000) + 1 лот Y (10 * 45 = 450) = 5450 > бюджета 5000
        Assert.Throws<InvalidOperationException>(() =>
            KalmanLotCalculator.FindMinimumValidPosition(
                minQtyY: 1m,
                beta: 1.13,
                lotSizeX: 100m,
                lotSizeY: 10m,
                priceX: 50m,
                priceY: 45m,
                maxTotalBudget: 5_000m));
    }
}
```

Нюансы учета денег в реальной торговле

- Спот против фьючерсов. Формула actualQtyX * priceX + actualQtyY * priceY считает полную номинальную стоимость (подходит для акций и крипто-спота). 
- Если вы торгуете фьючерсами или с плечом, передавайте в priceX и priceY размер гарантийного обеспечения (ГО) за 1 штуку актива вместо полной рыночной цены.


Нужно ли добавить метод ребалансировки уже открытой позиции (с порогом нечувствительности), чтобы робот корректно докупал или сокращал лоты при дрейфе beta?

