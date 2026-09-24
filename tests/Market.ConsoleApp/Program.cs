using Market.Core.PairTrading;

namespace Market.ConsoleApp;

internal static class Program
{
    static void Main(string[] args)
    {
    }

    static void KalmanFilterEstimatrSample()
    {
        // Симулируем цены двух коинтегрированных активов (например, BTC и ETH)
        // АssetY в среднем в 1.5 раза дороже AssetX + шум
        double[] assetX = { 100, 101, 100, 102, 104, 103, 105, 106, 104, 105 };
        double[] assetY = { 150, 152, 149, 153, 155, 156, 158, 159, 155, 157 };

        // Инициализация фильтра Калмана для оценки беты
        // Начальное предположение: Beta = 1.0, Ковариация ошибки = 1.0
        KalmanFilterEstimator kalman = new KalmanFilterEstimator(initialState: 1.0, initialErrorCovariance: 1.0);

        Console.WriteLine("Шаг\tЦена X\tЦена Y\tДинамическая Beta\tСпред (Spread)");
        Console.WriteLine("----------------------------------------------------------------------");

        for (int i = 0; i < assetX.Length; i++)
        {
            double x = assetX[i];
            double y = assetY[i];

            // Обновляем фильтр текущими ценами и получаем новую бету
            double currentBeta = kalman.Update(x, y);

            // Расчет текущего спреда: Y - Beta * X
            double spread = y - (currentBeta * x);

            Console.WriteLine($"{i + 1}\t{x:F1}\t{y:F1}\t{currentBeta:F4}\t\t{spread:F4}");
        }
    }

    static void KalmanPairsEngineSample()
    {
        // Симулируем цены: в середине массив содержит аномальное расширение спреда
        double[] assetX = { 100, 101, 100, 102, 101, 102, 103, 104, 103, 102 };
        double[] assetY = { 150, 152, 149, 153, 162, 164, 155, 156, 154, 153 }; // На шагах 5 и 6 искусственный выброс вверх

        // Инициализируем робота
        var tradingEngine = new KalmanPairsEngine(initialBeta: 1.5);

        Console.WriteLine("Шаг\tЦена X\tЦена Y\tBeta\tСпред\tZ-Score\tСигнал");
        Console.WriteLine("----------------------------------------------------------------------");

        for (int i = 0; i < assetX.Length; i++)
        {
            var result = tradingEngine.ProcessTick(assetX[i], assetY[i]);

            Console.WriteLine($"{i + 1}\t{assetX[i]:F1}\t{assetY[i]:F1}\t{result.Beta:F3}\t{result.Spread:F2}\t{result.ZScore:F2}\t{result.Signal}");
        }
    }

    static void HftPairsEngineSample()
    {
        var engine = new HftPairsEngine(initialBeta: 1.5);

        // Имитируем 10 секунд торгов. На 6-й секунде коинтеграция рушится (спред улетает и не возвращается)
        double[] assetX = { 100.0, 100.2, 100.1, 100.3, 100.4, 100.5, 100.6, 100.7, 100.8, 100.9 };
        double[] assetY = { 150.0, 150.1, 150.3, 150.2, 150.8, 153.0, 156.0, 159.0, 162.0, 165.0 };

        Console.WriteLine("Sec\tSpread\tZ-Score\tKappa (Speed)\tHalf-Life(sec)\tSignal");
        Console.WriteLine("---------------------------------------------------------------------------------");

        for (int i = 0; i < assetX.Length; i++)
        {
            var r = engine.ProcessSecond(assetX[i], assetY[i]);

            string halfLifeStr = r.IsCointegrated ? $"{r.HalfLifeSeconds:F1}s" : "INF";
            Console.WriteLine($"{i + 1}\t{r.Spread:F2}\t{r.ZScore:F2}\t{r.Kappa:F4}\t\t{halfLifeStr}\t\t{r.Signal}");
        }
    }

    static void HftOrderBookEngineample()
    {
        var engine = new HftOrderBookEngine(initialBeta: 1.5);

        // Имитируем тики со стаканом: { Bid, Ask }
        var assetX = new[] { new OrderBookTop(100.0, 100.1), new OrderBookTop(100.2, 100.3), new OrderBookTop(100.1, 100.2) };
        var assetY = new[] { new OrderBookTop(150.0, 150.2), new OrderBookTop(152.0, 152.3), new OrderBookTop(149.8, 150.0) };

        Console.WriteLine("Sec\tMid_X\tMid_Y\tBeta\tZ-Score Buy\tZ-Score Sell\tSignal");
        Console.WriteLine("-----------------------------------------------------------------------------------------");

        for (int i = 0; i < assetX.Length; i++)
        {
            var r = engine.ProcessSecond(assetX[i], assetY[i]);
            Console.WriteLine($"{i + 1}\t{assetX[i].Mid:F1}\t{assetY[i].Mid:F1}\t{r.Beta:F3}\t{r.ZScoreBuy:F2}\t\t{r.ZScoreSell:F2}\t\t{r.Signal}");
        }
    }
    static void HftVwapEngineSample()
    {
        // Инициализируем робота. Задаем объем, который мы хотим торговать по каждому активу
        double tradeVolumeY = 5.0; // Например, 5 лотов/монет Asset Y
        double initialBeta = 1.5;
        // Объем для X будет динамически рассчитываться как: tradeVolumeY / Beta

        var engine = new HftVwapEngine(initialBeta: initialBeta, tradeVolumeY: tradeVolumeY);

        // Имитируем глубокий стакан (L2) для Актива X (Цена, Объем)
        var orderBookX = new OrderBookDepth(
            bids: new List<OrderBookLevel> { new(100.0, 2.0), new(99.9, 4.0), new(99.8, 10.0) },
            asks: new List<OrderBookLevel> { new(100.1, 1.5), new(100.2, 3.5), new(100.3, 8.0) }
        );

        // Имитируем глубокий стакан (L2) для Актива Y (Цена, Объем)
        var orderBookY = new OrderBookDepth(
            bids: new List<OrderBookLevel> { new(150.0, 1.0), new(149.8, 3.0), new(149.5, 5.0) },
            asks: new List<OrderBookLevel> { new(150.2, 2.0), new(150.4, 4.0), new(150.6, 6.0) }
        );

        // Тестируем расчет VWAP и фильтрацию
        var r = engine.ProcessSecond(orderBookX, orderBookY);

        Console.WriteLine($"Текущая Beta: {r.Beta:F3}");
        Console.WriteLine($"Требуемый объем Y: {tradeVolumeY} лотов. Реальный VWAP Ask Y: {r.VwapAskY:F2}, Bid Y: {r.VwapBidY:F2}");
        Console.WriteLine($"Требуемый объем X: {tradeVolumeY / r.Beta:F2} лотов. Реальный VWAP Ask X: {r.VwapAskX:F2}, Bid X: {r.VwapBidX:F2}");
        Console.WriteLine($"-----------------------------------------------------------------------------------------");
        Console.WriteLine($"Z-Score Buy (Вход в лонг Y): {r.ZScoreBuy:F2}");
        Console.WriteLine($"Z-Score Sell (Вход в шорт Y): {r.ZScoreSell:F2}");
        Console.WriteLine($"Сигнал: {r.Signal}");
    }

    static void HftFeesEngineSample()
    {
        // Настройки комиссий: например, 0.04% (0.0004) для Актива X и 0.05% (0.0005) для Актива Y
        double feeRateX = 0.0004;
        double feeRateY = 0.0005;
        double tradeVolumeY = 5.0;

        var engine = new HftFeesEngine(initialBeta: 1.5, tradeVolumeY: tradeVolumeY, feeRateX: feeRateX, feeRateY: feeRateY);

        // Стакан X (Цена, Объем)
        var orderBookX = new OrderBookDepth(
            bids: new List<OrderBookLevel> { new(100.0, 10.0) },
            asks: new List<OrderBookLevel> { new(100.1, 10.0) }
        );

        // Стакан Y: Симулируем ситуацию, где спред расширился (Z-Score высокий), 
        // но из-за комиссий алгоритм должен проигнорировать этот вход
        var orderBookY = new OrderBookDepth(
            bids: new List<OrderBookLevel> { new(151.2, 10.0) },
            asks: new List<OrderBookLevel> { new(151.5, 10.0) }
        );

        var r = engine.ProcessSecond(orderBookX, orderBookY);

        Console.WriteLine($"Чистый Z-Score Sell (Без комиссий): {r.RawZScoreSell:F2}");
        Console.WriteLine($"Скорректированный Z-Score Sell (С учетом комиссий): {r.ZScoreSellWithFees:F2}");
        Console.WriteLine($"Суммарная стоимость комиссий на круг (в единицах Y): {r.TotalFeesInY:F4}");
        Console.WriteLine($"Сигнал: {r.Signal}");
    }
}
