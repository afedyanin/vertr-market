Коэффициент хеджирования (Hedge Ratio) в парном трейдинге рассчитывается как коэффициент наклона ($\beta$) линейной регрессии между ценами двух активов, где цена одного актива выступает зависимой переменной ($Y$), а цена другого — независимой ($X$).
Формула расчета коэффициента методом наименьших квадратов (OLS):
$$\beta = \frac{\text{Cov}(X, Y)}{\text{Var}(X)}$$ 
Ниже представлена готовая реализация на языке C# без использования тяжелых сторонних библиотек.
------------------------------
## 1. Подготовка структуры данных
Для удобства работы создадим простой класс, который будет хранить исторические цены закрытия для обоих активов.

public class PricePair
{
    public double PriceA { get; set; } // Актив Y (Зависимый)
    public double PriceB { get; set; } // Актив X (Независимый)

    public PricePair(double priceA, double priceB)
    {
        PriceA = priceA;
        PriceB = priceB;
    }
}

## 2. Расчет ковариации и дисперсии
Чтобы найти $\beta$, нам нужно вычислить средние значения, дисперсию независимого актива и ковариацию между ними.

public static class PairTradingMath
{
    public static double CalculateHedgeRatio(List<PricePair> data)
    {
        if (data == null || data.Count < 2)
        {
            throw new ArgumentException("Для расчета необходимо как минимум 2 точки данных.");
        }

        int n = data.Count;
        
        // 1. Находим средние значения (Mean)
        double sumX = 0;
        double sumY = 0;
        foreach (var pair in data)
        {
            sumY += pair.PriceA; // Y
            sumX += pair.PriceB; // X
        }
        double meanY = sumY / n;
        double meanX = sumX / n;

        // 2. Вычисляем ковариацию и дисперсию
        double covariance = 0;
        double varianceX = 0;

        foreach (var pair in data)
        {
            double diffX = pair.PriceB - meanX;
            double diffY = pair.PriceA - meanY;

            covariance += diffX * diffY;
            varianceX += diffX * diffX;
        }

        // Защита от деления на ноль (если цена актива B не менялась)
        if (Math.Abs(varianceX) < 1e-9)
        {
            throw new DivideByZeroException("Дисперсия актива B близка к нулю. Расчет невозможен.");
        }

        // 3. Расчет Hedge Ratio (Beta)
        return covariance / varianceX;
    }
}

## 3. Пример использования кода
Пример заполнения данных и вызова метода расчета:

class Program
{
    static void Main()
    {
        // Имитация исторических цен (Актив A и Актив B)
        var historicalData = new List<PricePair>
        {
            new PricePair(100.5, 50.2),
            new PricePair(102.0, 51.0),
            new PricePair(101.2, 50.5),
            new PricePair(103.5, 51.8),
            new PricePair(105.0, 52.1)
        };

        try
        {
            double hedgeRatio = PairTradingMath.CalculateHedgeRatio(historicalData);
            Console.WriteLine($"Рассчитанный Hedge Ratio: {hedgeRatio:F4}");
            Console.WriteLine($"Стратегия: На 1 лот актива A (Y) нужно открыть {-hedgeRatio:F4} лотов актива B (X).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }
}

------------------------------
## ⚠️ Важные нюансы для практики

* Логарифмические цены: На практике OLS часто применяют не к абсолютным ценам, а к их натуральным логарифмам $\ln(Price)$. Это делает коэффициент устойчивым к изменению масштаба цен и позволяет интерпретировать Hedge Ratio как процентное соотношение размеров позиций.
* Какая переменная X, а какая Y?: Традиционно в качестве $X$ выбирают более ликвидный или менее волатильный актив. Изменение выбора $X$ и $Y$ изменит итоговое значение коэффициента.
* Альтернативные методы: Если спред между активами нестабилен (коинтеграция нарушается), вместо классического OLS используют метод TLS (Total Least Squares), Principal Component Analysis (PCA) или фильтр Калмана (Kalman Filter) для динамического перерасчета Hedge Ratio в реальном времени.

------------------------------
## ✅ Итоговый результат
В парном трейдинге Hedge Ratio рассчитывается через классический метод наименьших квадратов (OLS). Написанный код на C# эффективно находит этот коэффициент, вычисляя отношение ковариации двух рядов цен к дисперсии ведущего актива.
Если вам интересно, я могу показать, как реализовать динамический расчет Hedge Ratio через фильтр Калмана на C# или как добавить проверку на коинтеграцию (тест ADF) для этой пары.

