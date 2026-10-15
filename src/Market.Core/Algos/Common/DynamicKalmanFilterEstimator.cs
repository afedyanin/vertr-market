namespace Market.Core.Algos.Common;

/*
СОБЫТИЙНЫЙ ДИНАМИЧЕСКИЙ КАЛМАН (LAV): принимает асинхронные тики любого из инструментов, 
хранит последнее состояние стакана партнера, вычисляет реальный dt между событиями для масштабирования QBeta 
и накладывает штраф за "устаревание" данных на RBeta.
*/

public class DynamicKalmanFilterEstimator
{
    private readonly double _q; // Волатильность процесса (насколько быстро бета может меняться)
    private readonly double _r; // Волатильность измерения (рыночный шум в спреде)

    public double Beta { get; private set; }
    public double P { get; private set; }
    public double H { get; private set; }
    public double S { get; private set; }
    public double K { get; private set; }
    public double Spread { get; private set; }
    public double Sigma { get; private set; }
    public double ZScore => Sigma > 0 ? Spread / Sigma : 0;

    private double? _priceX;
    private double? _priceY;

    private DateTime _lastEventTime = DateTime.MinValue;
    private DateTime _lastUpdateXTime = DateTime.MinValue;
    private DateTime _lastUpdateYTime = DateTime.MinValue;


    public DynamicKalmanFilterEstimator(
        double initialBeta = 1.0,
        double initialErrorCovariance = 1.0,
        double q = 0.00005,
        double r = 0.01)
    {
        // Настройки чувствительности (гиперпараметры)
        _q = q; // Маленький Q означает, что истинное соотношение активов меняется медленно
        _r = r; // R определяет уровень случайного шума в ценах

        Beta = initialBeta;
        P = initialErrorCovariance;
    }

    public bool HasValidData => _priceX.HasValue && _priceY.HasValue;

    public void UpdateX(double priceX, DateTime time)
    {
        // Пропускаем невалидные цены: иначе NaN/Infinity необратимо «отравит» Beta и Sigma.
        if (!double.IsFinite(priceX))
        {
            return;
        }

        _priceX = priceX;
        _lastUpdateXTime = time;
        ProcessTick(time);
    }

    public void UpdateY(double priceY, DateTime time)
    {
        if (!double.IsFinite(priceY))
        {
            return;
        }

        _priceY = priceY;
        _lastUpdateYTime = time;
        ProcessTick(time);
    }

    private void ProcessTick(DateTime timestamp)
    {
        if (!HasValidData)
        {
            _lastEventTime = timestamp;
            return;
        }

        // Расчет динамического шага времени (dt)
        var dt = _lastEventTime == DateTime.MinValue ? 0 : (timestamp - _lastEventTime).TotalSeconds;
        _lastEventTime = timestamp;

        // Защита от одновременных или сетевых пакетов из одной микросекунды
        if (dt < 0.000_001)
        {
            dt = 0.000_001;
        }

        // --- ШАГ ПРЕДСКАЗАНИЯ КАЛМАНА (Predict) с учетом dt ---
        // Шум процесса Q масштабируется по времени
        P += _q * dt;

        // Измерение (Measurement)
        H = _priceX!.Value;

        // --- ДИНАМИЧЕСКИЙ ШТРАФ STALE DATA (Корректировка R) ---
        // Считаем, как давно обновлялся каждый из стаканов относительно текущего события
        var timeSinceLastX = (timestamp - _lastUpdateXTime).TotalSeconds;
        var timeSinceLastY = (timestamp - _lastUpdateYTime).TotalSeconds;
        var maxStaleTime = Math.Max(timeSinceLastX, timeSinceLastY);

        // Чем старее данные одного из стаканов, тем сильнее мы раздуваем R для текущего шага
        var rDynamic = _r + (maxStaleTime * 0.05);

        // Ковариация инновации (S = H*P*H^T + R)
        S = H * P * H + rDynamic;

        // Коэффициент усиления Калмана (Kalman Gain)
        K = (P * H) / S;

        // Расчет ошибки прогноза (расхождение между реальным Y и прогнозным)
        Spread = _priceY!.Value - Beta * _priceX.Value;

        // Обновляем бету на основе ошибки с весом коэффициента Калмана
        Beta += K * Spread;

        // Обновляем ковариацию ошибки для следующего шага
        P = (1.0 - K * H) * P;

        Sigma = Math.Sqrt(S);
    }
}
