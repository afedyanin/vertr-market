using Accord.Statistics.Distributions.Univariate;
using Accord.Statistics.Models.Markov;
using Accord.Statistics.Models.Markov.Learning;
using Accord.Statistics.Models.Markov.Topology;

namespace Vertr.Market.Application.Tests.Candles;

/*

Forward: Это архитектура модели, где состояния идут только вперед (подходит для временных рядов).
NormalDistribution: Мы предполагаем, что доходности на рынке распределены «нормально». 
Если данные имеют «толстые хвосты» (как крипта), иногда используют распределение Стьюдента. 

*/

public class HMMSetup
{
    [Obsolete("Move to new version")]
    public void InitializeModel()
    {
        // 1. Определяем количество скрытых состояний (например, 2)
        var states = 2;

        // 2. Создаем модель с нормальным (Гауссовым) распределением для цен/доходностей
        // Каждое состояние будет иметь свое среднее значение и стандартное отклонение
        var model = new HiddenMarkovModel<NormalDistribution>(new Forward(states), new NormalDistribution());


        // 3. Настройка алгоритма обучения (Baum-Welch)
        _ = new BaumWelchLearning<NormalDistribution>(model)
        {
            Tolerance = 0.0001, // Точность обучения
            MaxIterations = 100    // Максимальное количество итераций
        };

        Console.WriteLine("Модель HMM инициализирована и готова к обучению.");
    }
}