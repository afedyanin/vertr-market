namespace Market.Core.PairTrading;

public class HftPairsEngine
{
#pragma warning disable CA1805 // Do not initialize unnecessarily
    // Калман 1: Оценка беты активов
    private double _beta;
    private double _pBeta = 1.0;
    private const double QBeta = 0.00005; // На секундах бета почти статична
    private const double RBeta = 0.01;

    // Калман 2: Онлайн-оценка AR(1) для спреда (Модель Орнштейна-Уленбека)
    private double _prevSpread = 0.0;
    private double _phi = 0.9; // Авторегрессионный коэффициент (должен быть < 1.0 для коинтеграции)
    private double _pPhi = 0.1;
    private const double QPhi = 0.001;  // Чувствительность к изменению скорости возврата
    private const double RPhi = 0.05;

    private bool _hasPosition = false;
    private int _secondsInTrade = 0;

#pragma warning restore CA1805 // Do not initialize unnecessarily

    public HftPairsEngine(double initialBeta = 1.0)
    {
        _beta = initialBeta;
    }

    public TickResult ProcessSecond(double priceX, double priceY)
    {
        // 1. Обновляем Бету и вычисляем текущий Спред
        _pBeta += QBeta;
        double sBeta = priceX * _pBeta * priceX + RBeta;
        double kBeta = (_pBeta * priceX) / sBeta;
        double spread = priceY - (_beta * priceX);
        _beta += kBeta * spread;
        _pBeta = (1.0 - kBeta * priceX) * _pBeta;

        // 2. Оценка коинтеграции и скорости возврата (OU-процесс) через AR(1) спреда: Спред_t = Phi * Спред_{t-1}
        double kappa = 0.0;
        double halfLife = double.PositiveInfinity;
        bool isCointegrated = false;

        if (_prevSpread != 0.0)
        {
            _pPhi += QPhi;
            double sPhi = _prevSpread * _pPhi * _prevSpread + RPhi;
            double kPhi = (_pPhi * _prevSpread) / sPhi;

            // Корректируем Phi на основе того, насколько спред последовал за предыдущим значением
            _phi += kPhi * (spread - (_phi * _prevSpread));
            _pPhi = (1.0 - kPhi * _prevSpread) * _pPhi;

            // Ограничиваем Phi физическими границами во избежание взрыва алгоритма
            _phi = Math.Clamp(_phi, -0.999, 1.5);

            // Переводим параметр AR(1) в непрерывную скорость возврата OU-процесса (Kappa)
            // Если Phi >= 1, ряд нестационарен (коинтеграция разрушена)
            if (_phi > 0 && _phi < 1.0)
            {
                kappa = -Math.Log(_phi); // Дискретный шаг = 1 секунда, delta_t не нужна
                halfLife = Math.Log(2.0) / kappa;
                isCointegrated = true;
            }
        }

        double sigma = Math.Sqrt(sBeta);
        double zScore = spread / sigma;

        // 3. Логика сигналов с контролем полураспада
        string signal = "HOLD";

        if (_hasPosition)
        {
            _secondsInTrade++;

            // СИГНАЛ ВЫХОДА ПО ПОЛУРАСПАДУ (Time Stop): 
            // Если позиция висит дольше, чем 2-3 периода полураспада, математическое ожидание прибыли исчезает.
            // Либо если коинтеграция полностью исчезла (isCointegrated == false)
            if (!isCointegrated || _secondsInTrade > (halfLife * 2.5))
            {
                signal = "FORCE EXIT (BREAKDOWN/TIMEOUT)";
                _hasPosition = false;
                _secondsInTrade = 0;
            }
            else if (Math.Abs(zScore) < 0.5)
            {
                signal = "EXIT (TAKE PROFIT)";
                _hasPosition = false;
                _secondsInTrade = 0;
            }
        }
        else
        {
            // ВХОД: Входим ТОЛЬКО если есть подтвержденная коинтеграция
            if (isCointegrated && halfLife > 1.0 && halfLife < 60.0) // Полураспад должен быть адекватным (например, от 1 до 60 сек)
            {
                if (zScore > 2.0)
                {
                    signal = "ENTER SHORT Y / LONG X";
                    _hasPosition = true;
                    _secondsInTrade = 0;
                }
                else if (zScore < -2.0)
                {
                    signal = "ENTER LONG Y / SHORT X";
                    _hasPosition = true;
                    _secondsInTrade = 0;
                }
            }
        }

        _prevSpread = spread;

        return new TickResult
        {
            Spread = spread,
            ZScore = zScore,
            Kappa = kappa,
            HalfLifeSeconds = halfLife,
            IsCointegrated = isCointegrated,
            Signal = signal
        };
    }
}

public struct TickResult
{
    public double Spread { get; set; }
    public double ZScore { get; set; }
    public double Kappa { get; set; }
    public double HalfLifeSeconds { get; set; }
    public bool IsCointegrated { get; set; }
    public string Signal { get; set; }
}
