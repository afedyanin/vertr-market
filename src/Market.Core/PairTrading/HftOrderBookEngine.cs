namespace Market.Core.PairTrading;

public class HftOrderBookEngine
{
    private double _beta;
    private double _pBeta = 1.0;
    private const double QBeta = 0.00005;
    private const double RBeta = 0.01;

    // Переменные для оценки стационарности (Калман 2)
#pragma warning disable CA1805 // Do not initialize unnecessarily
    private double _prevMidSpread = 0.0;
#pragma warning restore CA1805 // Do not initialize unnecessarily
    private double _phi = 0.9;
    private double _pPhi = 0.1;

    private PositionState _currentPosition = PositionState.None;

    public HftOrderBookEngine(double initialBeta = 1.0)
    {
        _beta = initialBeta;
    }

    public HftResult ProcessSecond(OrderBookTop x, OrderBookTop y)
    {
        // 1. Обновляем Бету фильтра Калмана по СРЕДНИМ ценам (Mid Price), 
        // так как истинное справедливое соотношение активов не зависит от сиюминутного спреда bid/ask
        _pBeta += QBeta;
        double sBeta = x.Mid * _pBeta * x.Mid + RBeta;
        double kBeta = (_pBeta * x.Mid) / sBeta;
        double midSpread = y.Mid - (_beta * x.Mid);
        _beta += kBeta * midSpread;
        _pBeta = (1.0 - kBeta * x.Mid) * _pBeta;

        // 2. Оценка коинтеграции (процесс Орнштейна-Уленбека)
        bool isCointegrated = false;
        if (_prevMidSpread != 0.0)
        {
            _pPhi += 0.001;
            double sPhi = _prevMidSpread * _pPhi * _prevMidSpread + 0.05;
            _phi += (_pPhi * _prevMidSpread / sPhi) * (midSpread - (_phi * _prevMidSpread));
            _pPhi = (1.0 - (_pPhi * _prevMidSpread / sPhi) * _prevMidSpread) * _pPhi;
            if (_phi > 0 && _phi < 1.0)
            {
                isCointegrated = true;
            }
        }

        _prevMidSpread = midSpread;

        // 3. РАСЧЕТ РЕАЛЬНЫХ ТОРГОВЫХ СПРЕДОВ С УЧЕТОМ BID/ASK
        // Спред, если мы заходим в LONG Y и SHORT X (Покупаем Y по Ask, продаем X по Bid)
        double spreadBuyY_SellX = y.Ask - (_beta * x.Bid);

        // Спред, если мы заходим в SHORT Y и LONG X (Продаем Y по Bid, покупаем X по Ask)
        double spreadSellY_BuyX = y.Bid - (_beta * x.Ask);

        // Переводим в Z-Score, используя общую сигму фильтра Калмана
        double sigma = Math.Sqrt(sBeta);
        double zScoreBuy = spreadBuyY_SellX / sigma;
        double zScoreSell = spreadSellY_BuyX / sigma;

        // 4. ТОРГОВАЯ ЛОГИКА
        string signal = "HOLD";

        if (_currentPosition == PositionState.None && isCointegrated)
        {
            // Вход в Short Y / Long X имеет смысл, только если спред ПРОДАЖИ (Sell) превысил верхний порог.
            // Мы будем бить по стакану: продавать Y по Bid и покупать X по Ask.
            if (zScoreSell > 2.0)
            {
                signal = $"EXECUTE: SHORT Y @ {y.Bid} / LONG X @ {x.Ask}";
                _currentPosition = PositionState.ShortY_LongX;
            }
            // Вход в Long Y / Short X: покупаем Y по Ask, продаем X по Bid.
            else if (zScoreBuy < -2.0)
            {
                signal = $"EXECUTE: LONG Y @ {y.Ask} / SHORT X @ {x.Bid}";
                _currentPosition = PositionState.LongY_ShortX;
            }
        }
        else if (_currentPosition == PositionState.ShortY_LongX)
        {
            // Выход из позиции Short Y / Long X означает, что нам нужно КУПИТЬ Y (по Ask) и ПРОДАТЬ X (по Bid).
            // Поэтому мы отслеживаем zScoreBuy для выхода.
            if (zScoreBuy <= 0.2)
            {
                signal = $"EXIT: BUY Y @ {y.Ask} / SELL X @ {x.Bid} (TAKE PROFIT)";
                _currentPosition = PositionState.None;
            }
        }
        else if (_currentPosition == PositionState.LongY_ShortX)
        {
            // Выход из позиции Long Y / Short X: ПРОДАЕМ Y (по Bid) и ПОКУПАЕМ X (по Ask).
            if (zScoreSell >= -0.2)
            {
                signal = $"EXIT: SELL Y @ {y.Bid} / BUY X @ {x.Ask} (TAKE PROFIT)";
                _currentPosition = PositionState.None;
            }
        }

        return new HftResult { Beta = _beta, ZScoreBuy = zScoreBuy, ZScoreSell = zScoreSell, Signal = signal };
    }
}

public enum PositionState { None, LongY_ShortX, ShortY_LongX }

public struct HftResult
{
    public double Beta { get; set; }
    public double ZScoreBuy { get; set; }
    public double ZScoreSell { get; set; }
    public string Signal { get; set; }
}

public struct OrderBookTop
{
    public double Bid { get; }
    public double Ask { get; }
    public double Mid => (Bid + Ask) / 2.0;
    public double Spread => Ask - Bid;

    public OrderBookTop(double bid, double ask)
    {
        Bid = bid;
        Ask = ask;
    }
}
