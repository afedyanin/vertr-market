using Accord.Statistics.Models.Markov;
using Accord.Statistics.Models.Markov.Learning;
using Accord.Statistics.Models.Markov.Topology;
using Accord.Statistics.Distributions.Multivariate;

namespace Vertr.Market.Palyground.Tests.Hmm;

// https://github.com/accord-net/framework

/*
## 1. Концепция: Что мы ищем?
Рынок обычно делят на 2, 3 или 4 скрытых состояния:
Bull Market (Низкая волатильность, положительный тренд).
Bear Market (Высокая волатильность, отрицательный тренд).
Sideways/Flat (Низкая волатильность, отсутствие тренда).

## 2. Подготовка данных
HMM плохо работает с сырыми ценами. Ей нужны стационарные данные. Обычно подают:
Log Returns (Логарифмическая доходность).
Range (Разница между High и Low за период).
Volatility (Скользящее стандартное отклонение).

## 3. Как интерпретировать результат?
После обучения модель выдаст индексы состояний (0, 1, 2). 
Чтобы понять, какой из них «медвежий», нужно посмотреть на параметры распределения в каждом состоянии:
Состояние с высоким средним (Mean) и низкой дисперсией (Variance) = Бычий тренд.
Состояние с отрицательным средним и высокой дисперсией = Медвежий тренд.
Состояние с близким к нулю средним = Боковик.
Почему это лучше скользящих средних?
HMM учитывает вероятность перехода. Если рынок находится в «спокойном» состоянии, модель потребует более веских доказательств (сильного импульса), 
чтобы заявить о смене режима на «панику», что отсеивает рыночный шум.

*/

public class MarketRegimeDetector
{
    [Obsolete("Move to new version")]
    public void DetectRegimes(double[] logReturns)
    {
        // 1. Предположим, у нас есть 3 скрытых состояния: Бычий, Медвежий, Флэт
        var states = 3;

        // 2. Инициализируем модель, где каждое состояние описывается Гауссовым распределением
        // (так как доходности условно нормальны)
        var model = new HiddenMarkovModel<MultivariateNormalDistribution>(
            new Forward(states),
            new MultivariateNormalDistribution(1) // 1 входной параметр (log returns)
        );

        // 3. Обучаем модель методом Баума-Велша
        var teacher = new BaumWelchLearning<MultivariateNormalDistribution>(model);

        // Преобразуем данные в формат для обучения
        var observations = logReturns.Select(x => new[] { x }).ToArray();
        teacher.Run(observations);

        // 4. Предсказываем наиболее вероятную последовательность состояний (Алгоритм Витерби)
        var periods = model.Predict(observations);

        for (var i = 0; i < periods.Length; i++)
        {
            Console.WriteLine($"Шаг {i}: Профит {logReturns[i]:P2} -> Состояние рынка: {periods[i]}");
        }
    }
}
