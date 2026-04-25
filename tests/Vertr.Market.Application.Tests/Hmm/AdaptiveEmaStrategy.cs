namespace Vertr.Market.Application.Tests.Hmm;


/*
Основные стратегии адаптации:

## Изменение чувствительности: 
В режиме Trend (низкая волатильность) — увеличиваем периоды (например, EMA 20/50). Это помогает «сидеть» в тренде дольше и не вылетать на мелких коррекциях [1].
В режиме High Volatility — уменьшаем периоды (например, EMA 5/15), чтобы быстрее реагировать на резкие развороты.

## Фильтрация (Veto Power):
Если HMM определяет режим как Flat (боковик), стратегия просто игнорирует любые пересечения EMA. Это экономит депозит, так как скользящие средние приносят больше всего убытков именно в «пиле» [1].

## Адаптивный Stop-Loss:
В волатильном режиме (Regime 1) стоп-лосс стоит раздвигать шире, так как рыночный «шум» может случайно закрыть позицию.
*/

public class AdaptiveEmaStrategy
{
    // Параметры для разных режимов рынка
    private (int fast, int slow) _trendParams = (20, 50);   // Для спокойного тренда
    private (int fast, int slow) _volatileParams = (5, 15); // Для быстрой волатильности
    //private (int fast, int slow) _flatParams = (0, 0);      // 0 - признак запрета торгов

    public void ExecuteTradeLogic(int marketRegime, double currentPrice, double[] priceHistory)
    {
        (int fastPeriod, int slowPeriod) currentSettings;

        // Выбираем параметры на основе предсказанного HMM состояния
        switch (marketRegime)
        {
            case 0: // Допустим, это "Стабильный тренд"
                currentSettings = _trendParams;
                Console.WriteLine("Режим: ТРЕНД. Используем консервативные EMA.");
                break;

            case 1: // "Высокая волатильность"
                currentSettings = _volatileParams;
                Console.WriteLine("Режим: ВОЛАТИЛЬНОСТЬ. Используем быстрые EMA.");
                break;

            default: // "Боковик / Неопределенность"
                Console.WriteLine("Режим: ФЛЭТ. Торговля приостановлена.");
                return;
        }

        // Расчет адаптивных EMA
        var fastEma = CalculateEma(priceHistory, currentSettings.fastPeriod);
        var slowEma = CalculateEma(priceHistory, currentSettings.slowPeriod);

        // Логика входа (пересечение)
        if (fastEma > slowEma)
        {
            Console.WriteLine("Сигнал: BUY");
        }
    }

    private double CalculateEma(double[] prices, int period)
    {
        // Здесь используется метод расчета EMA из предыдущих ответов
        return prices.Last(); // Заглушка
    }
}