using Tinkoff.InvestApi;

namespace Market.Gateways.Tinvest;

public class TinvestSettings
{
    public InvestApiSettings? InvestApiSettings { get; set; }

    public bool DataStreamEnabled { get; set; }

    public bool TradesStreamEnabled { get; set; }

    public int OrderBookDepth { get; set; }

    public string[] Subscriptions { get; set; } = [];

    public string OutputDirectory { get; set; } = "tinvest";

    public string StaticDataDirectory { get; set; } = "JsonStaticData";

    public TimeSpan TradeAggergationInterval { get; set; } = TimeSpan.FromSeconds(5);

    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// Таймаут запроса к T-Invest на выставление/отмену ордера.
    /// <see cref="TimeSpan.Zero"/> отключает таймаут.
    /// </summary>
    public TimeSpan OrderRequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Максимальный возраст торгового сигнала. Устаревшие сигналы
    /// отбрасываются, чтобы не входить по уже ушедшему спреду.
    /// <see cref="TimeSpan.Zero"/> отключает проверку.
    /// </summary>
    public TimeSpan SignalMaxAge { get; set; } = TimeSpan.FromSeconds(5);
}
