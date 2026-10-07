namespace Market.Core.Algos.Common;

// Оценка коинтеграции и скорости возврата (OU-процесс) через AR(1) спреда: Спред_t = Phi * Спред_{t-1}
public class CointegrationEstimator
{
    // Онлайн-оценка AR(1) для спреда (Модель Орнштейна-Уленбека)
    private double _prevSpread = 0.0;
    private double _phi = 0.9; // Авторегрессионный коэффициент (должен быть < 1.0 для коинтеграции)
    private double _p = 0.1;
    private readonly double _q = 0.001;  // Чувствительность к изменению скорости возврата
    private readonly double _r = 0.05;

    public double HalfLife { get; private set; } = double.PositiveInfinity;
    public bool IsCointegrated { get; private set; } = false;

    public void Update(double spread)
    {
        HalfLife = double.PositiveInfinity;
        IsCointegrated = false;

        if (_prevSpread != 0.0)
        {
            _p += _q;
            var s = _prevSpread * _p * _prevSpread + _r;
            var k = (_p * _prevSpread) / s;

            // Корректируем Phi на основе того, насколько спред последовал за предыдущим значением
            _phi += k * (spread - (_phi * _prevSpread));
            _p = (1.0 - k * _prevSpread) * _p;

            // Ограничиваем Phi физическими границами во избежание взрыва алгоритма
            _phi = Math.Clamp(_phi, -0.999, 1.5);

            // Переводим параметр AR(1) в непрерывную скорость возврата OU-процесса (Kappa)
            // Если Phi >= 1, ряд нестационарен (коинтеграция разрушена)
            if (_phi > 0 && _phi < 1.0)
            {
                var kappa = -Math.Log(_phi); // Дискретный шаг = 1 секунда, delta_t не нужна
                HalfLife = Math.Log(2.0) / kappa;
                IsCointegrated = true;
            }
        }

        _prevSpread = spread;
    }
}
