namespace Vertr.Market.Application.Models;

// Выходная свеча (размер: ~64 байта)
public struct Candle
{
    public int AssetId;
    public DateTime OpenTime;
    public decimal Open;
    public decimal High;
    public decimal Low;
    public decimal Close;
    public decimal Volume;
    public bool IsInitialized;
}

// Класс-контейнер события для Disruptor.
// Экземпляры создаются ровно один раз при старте Ring Buffer и используются по кругу.
public sealed class CandleEvent
{
    public Candle Value;
}
