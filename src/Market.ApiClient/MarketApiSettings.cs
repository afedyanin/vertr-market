namespace Market.ApiClient;

public class MarketApiSettings
{
    public int AssetId { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string TcpHost { get; set; } = string.Empty;

    public int TcpPort { get; set; }

    public bool UseTcp { get; set; }

    public int MaxConcurrentConnections { get; set; } = 100;
}
