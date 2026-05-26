using Accord.Statistics.Distributions.Univariate;
using Accord.Statistics.Models.Markov;
using Accord.Statistics.Models.Markov.Learning;
using Accord.Statistics.Models.Markov.Topology;

namespace Vertr.Market.Palyground.Tests.Hmm;


/*
Ключевые моменты:
## Логарифмирование: Мы используем Math.Log(prices[i] / prices[i - 1]). Это критично, так как HMM ожидает данные, колеблющиеся вокруг нуля, а не растущий график цены.
## Массив массивов: Accord.NET требует данные в формате double[][], где внутренний массив представляет собой вектор признаков (в нашем случае это один признак — доходность).
## Predict: Метод Predict использует алгоритм Витерби для восстановления всей цепочки состояний. Для принятия решения «здесь и сейчас» мы берем последний элемент массива.
Важно: Перед использованием Predict убедитесь, что у вас достаточно данных в окне (минимум 30–50 свечей), иначе модель может выдать некорректный режим из-за недостатка контекста. 
*/

[Obsolete("Move to new version")]
public class HmmTrainer
{
    public HiddenMarkovModel<NormalDistribution> TrainMarketModel(double[] prices, int states)
    {
        // 1. Превращаем цены в логарифмические доходности
        // Формула: ln(P_t / P_{t-1})
        var logReturns = new double[prices.Length - 1][];

        for (var i = 1; i < prices.Length; i++)
        {
            var r = Math.Log(prices[i] / prices[i - 1]);
            logReturns[i - 1] = new[] { r };
        }

        // 2. Инициализируем модель HMM
        // Используем нормальное распределение для доходностей
        var model = new HiddenMarkovModel<NormalDistribution>(
            new Forward(states),
            new NormalDistribution()
        );

        // 3. Настраиваем алгоритм обучения Баума-Велша
        var teacher = new BaumWelchLearning<NormalDistribution>(model)
        {
            Tolerance = 1e-6,
            Iterations = 100
        };

        // 4. Обучаем модель на подготовленных данных
        teacher.Run(logReturns);

        return model;
    }

    // Вспомогательный метод для определения режима последней свечи
    public double PredictCurrentRegime(HiddenMarkovModel<NormalDistribution> model, double[] prices)
    {
        // Подготавливаем данные так же, как для обучения
        var observations = new double[prices.Length - 1][];
        for (var i = 1; i < prices.Length; i++)
        {
            observations[i - 1] = new[] { Math.Log(prices[i] / prices[i - 1]) };
        }

        // Предсказываем наиболее вероятную последовательность состояний
        var states = model.Predict(observations);

        // Возвращаем последнее состояние
        return states.Last();
    }
}